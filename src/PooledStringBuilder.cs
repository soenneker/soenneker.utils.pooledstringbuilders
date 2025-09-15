using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Soenneker.Utils.PooledStringBuilders;

/// <summary>
/// High-performance pooled string builder that uses <see cref="ArrayPool{T}"/> to minimize allocations.
/// Intended for short-lived, per-frame/per-render string building.
/// </summary>
public ref struct PooledStringBuilder
{
    private char[] _buffer;
    private int _pos;
#if DEBUG
    private bool _disposed;
#endif

    private const int _defaultCapacity = 128;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledStringBuilder(int capacity = _defaultCapacity)
    {
        _buffer = ArrayPool<char>.Shared.Rent(capacity > 0 ? capacity : _defaultCapacity);
        _pos = 0;
#if DEBUG
        _disposed = false;
#endif
    }

    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _pos;
    }

    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _buffer.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _pos = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<char> AsSpan() => _buffer.AsSpan(0, _pos);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int required)
    {
        ThrowIfDisposed();
        if ((uint)required <= (uint)_buffer.Length) return;

        // Round up to next power of two to keep rent/copy count low.
        int newSize = RoundUpPow2(required);
        char[] newBuf = ArrayPool<char>.Shared.Rent(newSize);
        _buffer.AsSpan(0, _pos)
            .CopyTo(newBuf);
        ArrayPool<char>.Shared.Return(_buffer, clearArray: false);
        _buffer = newBuf;
    }

    /// <summary>Reserves space and returns a writable span of that length. Caller must fill it fully.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> AppendSpan(int length)
    {
        ThrowIfDisposed();
        if (length <= 0)
            return Span<char>.Empty;

        int newPos = _pos + length;
        EnsureCapacity(newPos);
        Span<char> dest = _buffer.AsSpan(_pos, length);
        _pos = newPos;
        return dest;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c)
    {
        ThrowIfDisposed();
        int i = _pos;
        if ((uint)i >= (uint)_buffer.Length)
        {
            EnsureCapacity(i + 1);
            i = _pos;
        }

        _buffer[i] = c;
        _pos = i + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(string? value)
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(value))
            return;

        ReadOnlySpan<char> src = value.AsSpan();
        Span<char> dest = AppendSpan(src.Length);
        src.CopyTo(dest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(ReadOnlySpan<char> value)
    {
        ThrowIfDisposed();
        if (value.Length == 0) return;
        value.CopyTo(AppendSpan(value.Length));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c1, char c2)
    {
        ThrowIfDisposed();
        Span<char> dest = AppendSpan(2);
        dest[0] = c1;
        dest[1] = c2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c1, char c2, char c3)
    {
        ThrowIfDisposed();
        Span<char> dest = AppendSpan(3);
        dest[0] = c1;
        dest[1] = c2;
        dest[2] = c3;
    }

    /// <summary>Append any value type implementing <see cref="ISpanFormattable"/> without allocations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append<T>(T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) where T : ISpanFormattable
    {
        ThrowIfDisposed();
        // Worst-case length guess to reduce retries (32 covers most primitives; dates may need more).
        var hint = 32;

        while (true)
        {
            Span<char> span = AppendSpan(hint);
            if (value.TryFormat(span, out int written, format, provider))
            {
                // Adjust back if our hint was larger than actually written
                _pos -= (hint - written);
                return;
            }

            // Not enough; roll back and grow, then retry.
            _pos -= hint;
            hint <<= 1;
            EnsureCapacity(_pos + hint);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine() => Append('\n');

    /// <summary>Appends a separator if the builder is not empty (useful for comma/space delimiting).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendSeparatorIfNotEmpty(char separator)
    {
        if (_pos != 0) 
            Append(separator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => new(_buffer, 0, _pos);

    /// <summary>Returns the built string and releases the buffer to the pool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToStringAndDispose(bool clear = false)
    {
        var s = new string(_buffer, 0, _pos);
        Dispose(clear);
        return s;
    }

    public void Dispose(bool clear)
    {
#if DEBUG
        if (_disposed) return;
        _disposed = true;
#endif
        char[]? buf = _buffer;

        if (buf is not null)
        {
            ArrayPool<char>.Shared.Return(buf, clearArray: clear);
            _buffer = null!;
            _pos = 0;
        }
    }

    public void Dispose() => Dispose(clear: false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundUpPow2(int v)
    {
        // clamp to positive
        if (v <= 0) 
            return _defaultCapacity;

        // next power-of-two (cap at Array.MaxLength-ish range)
        var x = (uint)(v - 1);
        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        x++;
        // Avoid excessively huge rents; ArrayPool may throw if extreme.
        const int max = 0x3FFFFFE0; // approx Array.MaxLength for char[]
        return (int)Math.Min(x, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
#if DEBUG
        if (_disposed) ThrowDisposed();
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowDisposed() => throw new ObjectDisposedException(nameof(PooledStringBuilder));
}
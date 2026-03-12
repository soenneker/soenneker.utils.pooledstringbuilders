using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Soenneker.Utils.PooledStringBuilders;

public ref partial struct PooledStringBuilder
{
    // Sentinel: if _buffer == DisposedSentinel => disposed
    private static readonly char[] _disposedSentinel = Array.Empty<char>();
    private static readonly ArrayPool<char> _pool = ArrayPool<char>.Shared;

    private char[]? _buffer; // null => default(ref struct) never initialized
    private int _pos;

    private const int _defaultCapacity = 128;

    /// <summary>
    /// Initializes a new pooled string builder with the specified initial capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity. If less than or equal to zero, uses the default capacity of 128.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledStringBuilder(int capacity = _defaultCapacity)
    {
        if (capacity <= 0)
            capacity = _defaultCapacity;

        _buffer = _pool.Rent(capacity);
        _pos = 0;
    }

    /// <summary>
    /// Gets the number of characters in the current builder.
    /// </summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _pos;
    }

    /// <summary>
    /// Gets the capacity of the internal buffer.
    /// </summary>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetBufferOrInit().Length;
    }

    /// <summary>
    /// Removes all characters from the builder without releasing the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        ThrowIfDisposed();
        _pos = 0;
    }

    /// <summary>
    /// Returns a read-only span over the current builder contents.
    /// </summary>
    /// <returns>A read-only span of the characters in the builder.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<char> AsSpan()
    {
        char[] buf = GetBufferOrInit();
        return buf.AsSpan(0, _pos);
    }

    /// <summary>
    /// Ensures the builder has at least the specified capacity.
    /// </summary>
    /// <param name="required">The minimum required capacity.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int required)
    {
        char[] buf = GetBufferOrInit();
        EnsureCapacityCore(buf, required);
    }

    /// <summary>
    /// Removes the specified number of characters from the end of the builder.
    /// </summary>
    /// <param name="count">The number of characters to remove. If greater than Length, Length is set to zero.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Shrink(int count)
    {
        ThrowIfDisposed();

        if (count <= 0)
            return;

        _pos = (uint)count > (uint)_pos ? 0 : _pos - count;
    }

    /// <summary>
    /// Returns the current contents as a string. The builder is not disposed.
    /// </summary>
    /// <returns>A new string containing the builder's characters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString()
    {
        char[] buf = GetBufferOrInit();
        return new string(buf, 0, _pos);
    }

    /// <summary>
    /// Returns the current contents as a string and returns the buffer to the pool.
    /// </summary>
    /// <param name="clear">If true, clears the buffer before returning it to the pool.</param>
    /// <returns>A new string containing the builder's characters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToStringAndDispose(bool clear = false)
    {
        char[] buf = GetBufferOrInit();

        string s = new(buf, 0, _pos);
        Dispose(clear);
        return s;
    }

    /// <summary>
    /// Returns the buffer to the pool. Call this when finished to avoid leaking pooled memory.
    /// </summary>
    /// <param name="clear">If true, clears the buffer before returning it to the pool.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose(bool clear)
    {
        char[]? buf = _buffer;

        if (buf is null || ReferenceEquals(buf, _disposedSentinel))
        {
            _buffer = _disposedSentinel;
            _pos = 0;
            return;
        }

        _buffer = _disposedSentinel;
        _pos = 0;

        _pool.Return(buf, clearArray: clear);
    }

    /// <summary>
    /// Returns the buffer to the pool without clearing it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Dispose(clear: false);

    /// <summary>
    /// Inserts a character at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert.</param>
    /// <param name="value">The character to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException">index is less than 0 or greater than Length.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert(int index, char value)
    {
        char[] buf = GetBufferOrInit();

        if ((uint)index > (uint)_pos)
            throw new ArgumentOutOfRangeException(nameof(index));

        int newPos = _pos + 1;
        if ((uint)newPos > (uint)buf.Length)
        {
            EnsureCapacityCore(buf, newPos);
            buf = _buffer!;
        }

        int tail = _pos - index;
        if (tail > 0)
            buf.AsSpan(index, tail).CopyTo(buf.AsSpan(index + 1, tail));

        buf[index] = value;
        _pos = newPos;
    }

    /// <summary>
    /// Inserts the characters from a span at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert.</param>
    /// <param name="value">The span of characters to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException">index is less than 0 or greater than Length.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert(int index, ReadOnlySpan<char> value)
    {
        char[] buf = GetBufferOrInit();

        if ((uint)index > (uint)_pos)
            throw new ArgumentOutOfRangeException(nameof(index));

        int len = value.Length;
        if (len == 0)
            return;

        int newPos = _pos + len;
        if ((uint)newPos > (uint)buf.Length)
        {
            EnsureCapacityCore(buf, newPos);
            buf = _buffer!;
        }

        int tail = _pos - index;
        if (tail > 0)
            buf.AsSpan(index, tail).CopyTo(buf.AsSpan(index + len, tail));

        value.CopyTo(buf.AsSpan(index));
        _pos = newPos;
    }

    /// <summary>
    /// Inserts a string at the specified index. Does nothing if the value is null or empty.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert.</param>
    /// <param name="value">The string to insert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert(int index, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            ThrowIfDisposed();
            return;
        }

        Insert(index, value.AsSpan());
    }

    // --------- internals ---------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char[] GetBufferOrInit()
    {
        char[]? buf = _buffer;

        if (buf is null)
        {
            _buffer = buf = _pool.Rent(_defaultCapacity);
            _pos = 0;
            return buf;
        }

        if (ReferenceEquals(buf, _disposedSentinel))
            ThrowDisposed();

        return buf;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacityCore(char[] current, int required)
    {
        if ((uint)required <= (uint)current.Length)
            return;

        int newSize = RoundUpPow2(required);
        char[] newBuf = _pool.Rent(newSize);

        current.AsSpan(0, _pos).CopyTo(newBuf);
        _pool.Return(current, clearArray: false);

        _buffer = newBuf;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (ReferenceEquals(_buffer, _disposedSentinel))
            ThrowDisposed();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundUpPow2(int v)
    {
        if (v <= 0)
            return _defaultCapacity;

        uint x = (uint)(v - 1);
        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        x++;

        const uint max = 0x3FFFFFE0; // approx Array.MaxLength for char[]
        if (x > max)
            return (int)max;

        return (int)x;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowDisposed() =>
        throw new ObjectDisposedException(nameof(PooledStringBuilder));
}
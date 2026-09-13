using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Soenneker.Utils.PooledStringBuilders;

/// <summary>
/// A stack-only string builder backed by caller-provided storage or a pooled array.
/// </summary>
public ref partial struct PooledStringBuilder
{
    // Disposed builders have an empty span, zero length, and this sentinel as their owner.
    private static readonly char[] _disposedSentinel = Array.Empty<char>();
    private Span<char> _chars;
    private char[]? _buffer; // null for default builders and caller-provided storage
    private int _pos;

    private const int _defaultCapacity = 128;

    /// <summary>Creates a builder with a rented buffer of at least the requested capacity.</summary>
    /// <param name="capacity">The initial capacity. Nonpositive values use 128 characters.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledStringBuilder(int capacity = _defaultCapacity)
    {
        if (capacity <= 0)
            capacity = _defaultCapacity;

        _buffer = ArrayPool<char>.Shared.Rent(capacity);
        _chars = _buffer;
        _pos = 0;
    }

    /// <summary>
    /// Creates an empty builder using caller-provided storage. Rents an array only when this storage is outgrown.
    /// </summary>
    /// <param name="initialBuffer">Storage that must remain valid for the lifetime of the builder.</param>
    /// <remarks>Existing characters are not part of the contents. The caller retains ownership of the storage.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledStringBuilder(Span<char> initialBuffer)
    {
        _chars = initialBuffer;
        _buffer = null;
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
        get
        {
            if (_chars.IsEmpty)
                Grow(_defaultCapacity);
            return _chars.Length;
        }
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
        ThrowIfDisposed();
        return _chars.Slice(0, _pos);
    }

    /// <summary>
    /// Ensures the builder has at least the specified capacity.
    /// </summary>
    /// <param name="required">The minimum required capacity.</param>
    /// <exception cref="ArgumentOutOfRangeException">The required capacity is negative.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int required)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(required);
        if (required > _chars.Length || _chars.IsEmpty)
            Grow(required);
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
        if (_pos == 0)
        {
            ThrowIfDisposed();
            return string.Empty;
        }

        return new string(_chars.Slice(0, _pos));
    }

    /// <summary>
    /// Returns the current contents as a string and returns the buffer to the pool.
    /// </summary>
    /// <param name="clear">If true, clears the current storage, including caller-provided memory, before releasing it.</param>
    /// <returns>A new string containing the builder's characters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToStringAndDispose(bool clear = false)
    {
        string s = ToString();
        Dispose(clear);
        return s;
    }

    /// <summary>
    /// Releases the current storage and returns a rented buffer to the pool. Caller-provided storage is never returned to the pool.
    /// </summary>
    /// <param name="clear">If true, clears the current storage, including caller-provided memory, before releasing it.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose(bool clear)
    {
        char[]? buf = _buffer;
        if (clear)
            _chars.Clear();
        _chars = default;
        _buffer = _disposedSentinel;
        _pos = 0;
        if (buf is not null && !ReferenceEquals(buf, _disposedSentinel))
            ArrayPool<char>.Shared.Return(buf);
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
        ThrowIfDisposed();

        if ((uint)index > (uint)_pos)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (_pos == _chars.Length)
        {
            GrowAndInsert(index, new ReadOnlySpan<char>(in value));
            return;
        }

        int tail = _pos - index;
        if (tail > 0)
            _chars.Slice(index, tail).CopyTo(_chars.Slice(index + 1, tail));

        _chars[index] = value;
        _pos++;
    }

    /// <summary>
    /// Inserts the characters from a span at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert.</param>
    /// <param name="value">The span of characters to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException">index is less than 0 or greater than Length.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert(int index, scoped ReadOnlySpan<char> value)
    {
        ThrowIfDisposed();

        if ((uint)index > (uint)_pos)
            throw new ArgumentOutOfRangeException(nameof(index));

        int len = value.Length;
        if (len == 0)
            return;

        if (len > _chars.Length - _pos)
        {
            GrowAndInsert(index, value);
            return;
        }

        if (value.Overlaps(_chars))
        {
            InsertOverlapping(index, value);
            return;
        }

        InsertCore(index, value);
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

        ThrowIfDisposed();
        if ((uint)index > (uint)_pos)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (value.Length > _chars.Length - _pos)
            GrowAndInsert(index, value.AsSpan());
        else
            InsertCore(index, value.AsSpan());
    }

    // --------- internals ---------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Initialize(int required)
    {
        _buffer = RentBuffer(required, _chars.Length, _buffer);
        _chars = _buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char[] RentBuffer(int required, int capacity, char[]? previous)
    {
        if (capacity == 0 && ReferenceEquals(previous, _disposedSentinel))
            ThrowDisposed();
        if ((uint)required > Array.MaxLength)
            throw new OutOfMemoryException();

        return ArrayPool<char>.Shared.Rent(capacity == 0 ? Math.Max(_defaultCapacity, required) : required);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReplaceBuffer(char[] buffer)
    {
        char[]? previous = _buffer;
        _buffer = buffer;
        _chars = buffer;
        if (previous is not null)
            ArrayPool<char>.Shared.Return(previous);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Grow(int required)
    {
        char[] buffer = GrowCore(_chars.Slice(0, _pos), required, _chars.Length, _buffer);
        ReplaceBuffer(buffer);
    }

    // Passing the contents by value lets the JIT keep the builder's fields in registers.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static char[] GrowCore(ReadOnlySpan<char> contents, int required, int capacity, char[]? previous)
    {
        char[] buffer = RentBuffer(required, capacity, previous);
        contents.CopyTo(buffer);
        return buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_chars.IsEmpty && ReferenceEquals(_buffer, _disposedSentinel))
            ThrowDisposed();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowDisposed() =>
        throw new ObjectDisposedException(nameof(PooledStringBuilder));
}

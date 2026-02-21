using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Soenneker.Utils.PooledStringBuilders;

public ref partial struct PooledStringBuilder
{
    // Conservative max lengths (no separators)
    private const int _int32MaxChars = 11;  // -2147483648
    private const int _uInt32MaxChars = 10; // 4294967295
    private const int _int64MaxChars = 20;  // -9223372036854775808
    private const int _uInt64MaxChars = 20; // 18446744073709551615

    /// <summary>
    /// Appends the string representation of a 32-bit signed integer using invariant culture.
    /// </summary>
    /// <param name="value">The value to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(int value)
    {
        char[] buf = GetBufferOrInit();
        int oldPos = _pos;
        int newPos = oldPos + _int32MaxChars;

        if ((uint)newPos > (uint)buf.Length)
        {
            EnsureCapacityCore(buf, newPos);
            buf = _buffer!;
        }

        // Format into the reserved max span then shrink to written count
        Span<char> dest = buf.AsSpan(oldPos, _int32MaxChars);

        if (!value.TryFormat(dest, out int written, provider: CultureInfo.InvariantCulture))
            ThrowUnreachable();

        _pos = oldPos + written;
    }

    /// <summary>
    /// Appends the string representation of a 32-bit unsigned integer using invariant culture.
    /// </summary>
    /// <param name="value">The value to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(uint value)
    {
        char[] buf = GetBufferOrInit();
        int oldPos = _pos;
        int newPos = oldPos + _uInt32MaxChars;

        if ((uint)newPos > (uint)buf.Length)
        {
            EnsureCapacityCore(buf, newPos);
            buf = _buffer!;
        }

        Span<char> dest = buf.AsSpan(oldPos, _uInt32MaxChars);

        if (!value.TryFormat(dest, out int written, provider: CultureInfo.InvariantCulture))
            ThrowUnreachable();

        _pos = oldPos + written;
    }

    /// <summary>
    /// Appends the string representation of a 64-bit signed integer using invariant culture.
    /// </summary>
    /// <param name="value">The value to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(long value)
    {
        char[] buf = GetBufferOrInit();
        int oldPos = _pos;
        int newPos = oldPos + _int64MaxChars;

        if ((uint)newPos > (uint)buf.Length)
        {
            EnsureCapacityCore(buf, newPos);
            buf = _buffer!;
        }

        Span<char> dest = buf.AsSpan(oldPos, _int64MaxChars);

        if (!value.TryFormat(dest, out int written, provider: CultureInfo.InvariantCulture))
            ThrowUnreachable();

        _pos = oldPos + written;
    }

    /// <summary>
    /// Appends the string representation of a 64-bit unsigned integer using invariant culture.
    /// </summary>
    /// <param name="value">The value to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(ulong value)
    {
        char[] buf = GetBufferOrInit();
        int oldPos = _pos;
        int newPos = oldPos + _uInt64MaxChars;

        if ((uint)newPos > (uint)buf.Length)
        {
            EnsureCapacityCore(buf, newPos);
            buf = _buffer!;
        }

        Span<char> dest = buf.AsSpan(oldPos, _uInt64MaxChars);

        if (!value.TryFormat(dest, out int written, provider: CultureInfo.InvariantCulture))
            ThrowUnreachable();

        _pos = oldPos + written;
    }

    /// <summary>
    /// Appends a character repeated the specified number of times.
    /// </summary>
    /// <param name="c">The character to append.</param>
    /// <param name="count">The number of times to append the character. If less than or equal to zero, nothing is appended.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c, int count)
    {
        if (count <= 0)
        {
            ThrowIfDisposed();
            return;
        }

        char[] buf = GetBufferOrInit();

        int oldPos = _pos;
        int newPos = oldPos + count;

        if ((uint)newPos > (uint)buf.Length)
        {
            EnsureCapacityCore(buf, newPos);
            buf = _buffer!;
        }

        buf.AsSpan(oldPos, count).Fill(c);
        _pos = newPos;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnreachable() =>
        throw new InvalidOperationException("Unexpected TryFormat failure.");
}
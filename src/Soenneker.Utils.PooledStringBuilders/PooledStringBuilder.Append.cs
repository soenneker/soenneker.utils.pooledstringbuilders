using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Soenneker.Utils.PooledStringBuilders;

public ref partial struct PooledStringBuilder
{
    // Conservative max lengths for numeric types (no separators)
    private const int _int32MaxChars = 11;  // -2147483648
    private const int _uInt32MaxChars = 10; // 4294967295
    private const int _int64MaxChars = 20;  // -9223372036854775808
    private const int _uInt64MaxChars = 20; // 18446744073709551615

    /// <summary>
    /// Appends space for the specified number of characters and returns a span for writing.
    /// </summary>
    /// <param name="length">The number of characters to reserve.</param>
    /// <returns>A span over the appended region. Empty if length is less than or equal to zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> AppendSpan(int length)
    {
        if (length <= 0)
        {
            ThrowIfDisposed(); // keep semantics: disposed still throws
            return Span<char>.Empty;
        }

        char[] buf = GetBufferOrInit();

        int oldPos = _pos;
        int newPos = oldPos + length;

        if ((uint)newPos > (uint)buf.Length)
        {
            EnsureCapacityCore(buf, newPos);
            buf = _buffer!; // updated by EnsureCapacityCore
        }

        _pos = newPos;
        return buf.AsSpan(oldPos, length);
    }

    /// <summary>
    /// Appends a single character.
    /// </summary>
    /// <param name="c">The character to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c)
    {
        char[] buf = GetBufferOrInit();

        int i = _pos;
        if ((uint)i >= (uint)buf.Length)
        {
            EnsureCapacityCore(buf, i + 1);
            buf = _buffer!;
        }

        buf[i] = c;
        _pos = i + 1;
    }

    /// <summary>
    /// Appends a string. Does nothing if the value is null or empty.
    /// </summary>
    /// <param name="value">The string to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            ThrowIfDisposed(); // match: disposed still throws even if no-op
            return;
        }

        char[] buf = GetBufferOrInit();

        int len = value.Length;
        int newPos = _pos + len;

        if ((uint)newPos > (uint)buf.Length)
        {
            EnsureCapacityCore(buf, newPos);
            buf = _buffer!;
        }

        value.AsSpan().CopyTo(buf.AsSpan(_pos));
        _pos = newPos;
    }

    /// <summary>
    /// Appends the characters from the specified read-only span.
    /// </summary>
    /// <param name="value">The span of characters to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(ReadOnlySpan<char> value)
    {
        if (value.Length == 0)
        {
            ThrowIfDisposed();
            return;
        }

        char[] buf = GetBufferOrInit();

        int len = value.Length;
        int newPos = _pos + len;

        if ((uint)newPos > (uint)buf.Length)
        {
            EnsureCapacityCore(buf, newPos);
            buf = _buffer!;
        }

        value.CopyTo(buf.AsSpan(_pos));
        _pos = newPos;
    }

    /// <summary>
    /// Appends two characters.
    /// </summary>
    /// <param name="c1">The first character.</param>
    /// <param name="c2">The second character.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c1, char c2)
    {
        char[] buf = GetBufferOrInit();

        int oldPos = _pos;
        int newPos = oldPos + 2;

        if ((uint)newPos > (uint)buf.Length)
        {
            EnsureCapacityCore(buf, newPos);
            buf = _buffer!;
        }

        buf[oldPos] = c1;
        buf[oldPos + 1] = c2;
        _pos = newPos;
    }

    /// <summary>
    /// Appends three characters.
    /// </summary>
    /// <param name="c1">The first character.</param>
    /// <param name="c2">The second character.</param>
    /// <param name="c3">The third character.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c1, char c2, char c3)
    {
        char[] buf = GetBufferOrInit();

        int oldPos = _pos;
        int newPos = oldPos + 3;

        if ((uint)newPos > (uint)buf.Length)
        {
            EnsureCapacityCore(buf, newPos);
            buf = _buffer!;
        }

        buf[oldPos] = c1;
        buf[oldPos + 1] = c2;
        buf[oldPos + 2] = c3;
        _pos = newPos;
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
    /// Appends the string representation of a span-formattable value.
    /// </summary>
    /// <typeparam name="T">The type of the value, must implement <see cref="ISpanFormattable"/>.</typeparam>
    /// <param name="value">The value to format and append.</param>
    /// <param name="format">The format to use.</param>
    /// <param name="provider">The format provider. Can be null for default formatting.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append<T>(T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : ISpanFormattable
    {
        char[] buf = GetBufferOrInit();

        int hint = 32;

        while (true)
        {
            int required = _pos + hint;
            if ((uint)required > (uint)buf.Length)
            {
                EnsureCapacityCore(buf, required);
                buf = _buffer!;
            }

            Span<char> dest = buf.AsSpan(_pos, hint);

            if (value.TryFormat(dest, out int written, format, provider))
            {
                _pos += written;
                return;
            }

            hint <<= 1;
        }
    }

    /// <summary>
    /// Appends a newline character.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine() => Append('\n');

    /// <summary>
    /// Appends a character followed by a newline.
    /// </summary>
    /// <param name="c">The character to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine(char c)
    {
        Append(c);
        Append('\n');
    }

    /// <summary>
    /// Appends a string followed by a newline.
    /// </summary>
    /// <param name="value">The string to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine(string? value)
    {
        Append(value);
        Append('\n');
    }

    /// <summary>
    /// Appends the characters from a span followed by a newline.
    /// </summary>
    /// <param name="value">The span of characters to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine(ReadOnlySpan<char> value)
    {
        Append(value);
        Append('\n');
    }

    /// <summary>
    /// Appends the string representation of a span-formattable value followed by a newline.
    /// </summary>
    /// <typeparam name="T">The type of the value, must implement <see cref="ISpanFormattable"/>.</typeparam>
    /// <param name="value">The value to format and append.</param>
    /// <param name="format">The format to use.</param>
    /// <param name="provider">The format provider. Can be null for default formatting.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine<T>(T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : ISpanFormattable
    {
        Append(value, format, provider);
        Append('\n');
    }

    /// <summary>
    /// Appends the separator character only if the builder already has content.
    /// </summary>
    /// <param name="separator">The separator character to append when the builder is not empty.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendSeparatorIfNotEmpty(char separator)
    {
        ThrowIfDisposed();
        if (_pos != 0)
            Append(separator);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnreachable() =>
        throw new InvalidOperationException("Unexpected TryFormat failure.");
}

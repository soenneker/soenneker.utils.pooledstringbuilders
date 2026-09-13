using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Soenneker.Utils.PooledStringBuilders;

public ref partial struct PooledStringBuilder
{
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

        int oldPos = _pos;
        Span<char> buf = _chars;

        if (length > buf.Length - oldPos)
        {
            if (buf.IsEmpty)
                Initialize(length);
            else
                Grow(checked(oldPos + length));
            buf = _chars;
        }

        Span<char> result = buf.Slice(oldPos, length);
        _pos = oldPos + length;
        return result;
    }

    /// <summary>
    /// Appends a single character.
    /// </summary>
    /// <param name="c">The character to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c)
    {
        Span<char> buf = _chars;
        int i = _pos;
        if ((uint)i >= (uint)buf.Length)
        {
            if (buf.IsEmpty)
                Initialize(_defaultCapacity);
            else
                Grow(i + 1);
            buf = _chars;
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
        Append(value.AsSpan());
    }

    /// <summary>
    /// Appends the characters from the specified read-only span.
    /// </summary>
    /// <param name="value">The span of characters to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(scoped ReadOnlySpan<char> value)
    {
        if (value.Length == 0)
        {
            ThrowIfDisposed();
            return;
        }

        int len = value.Length;
        Span<char> buf = _chars;

        if (len > buf.Length - _pos)
        {
            if (buf.IsEmpty)
            {
                Initialize(len);
                buf = _chars;
            }
            else
            {
                GrowAndAppend(value, appendNewline: false);
                return;
            }
        }

        value.CopyTo(buf.Slice(_pos));
        _pos += len;
    }

    /// <summary>
    /// Appends two characters.
    /// </summary>
    /// <param name="c1">The first character.</param>
    /// <param name="c2">The second character.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c1, char c2)
    {
        Span<char> destination = AppendSpan(2);
        destination[0] = c1;
        destination[1] = c2;
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
        Span<char> destination = AppendSpan(3);
        destination[0] = c1;
        destination[1] = c2;
        destination[2] = c3;
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

        int oldPos = _pos;
        Span<char> buf = _chars;

        if (count > buf.Length - oldPos)
        {
            if (buf.IsEmpty)
                Initialize(count);
            else
                Grow(checked(oldPos + count));
            buf = _chars;
        }

        buf.Slice(oldPos, count).Fill(c);
        _pos = oldPos + count;
    }

    /// <summary>
    /// Appends the string representation of a 32-bit signed integer using invariant culture.
    /// </summary>
    /// <param name="value">The value to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(int value)
    {
        Span<char> buf = GetIntegerBuffer((long)value, 11);
        value.TryFormat(buf.Slice(_pos), out int written, provider: CultureInfo.InvariantCulture);
        _pos += written;
    }

    /// <summary>
    /// Appends the string representation of a 32-bit unsigned integer using invariant culture.
    /// </summary>
    /// <param name="value">The value to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(uint value)
    {
        Span<char> buf = GetIntegerBuffer((ulong)value, 10);
        value.TryFormat(buf.Slice(_pos), out int written, provider: CultureInfo.InvariantCulture);
        _pos += written;
    }

    /// <summary>
    /// Appends the string representation of a 64-bit signed integer using invariant culture.
    /// </summary>
    /// <param name="value">The value to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(long value)
    {
        Span<char> buf = GetIntegerBuffer(value, 20);
        value.TryFormat(buf.Slice(_pos), out int written, provider: CultureInfo.InvariantCulture);
        _pos += written;
    }

    /// <summary>
    /// Appends the string representation of a 64-bit unsigned integer using invariant culture.
    /// </summary>
    /// <param name="value">The value to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(ulong value)
    {
        Span<char> buf = GetIntegerBuffer(value, 20);
        value.TryFormat(buf.Slice(_pos), out int written, provider: CultureInfo.InvariantCulture);
        _pos += written;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<char> GetIntegerBuffer(long value, int maxChars)
    {
        Span<char> buf = _chars;
        int available = buf.Length - _pos;
        if (available < maxChars)
        {
            bool negative = value < 0;
            ulong magnitude = negative ? unchecked(0UL - (ulong)value) : (ulong)value;
            if (!FitsInteger(magnitude, available - (negative ? 1 : 0)))
            {
                Grow(checked(_pos + maxChars));
                buf = _chars;
            }
        }

        return buf;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<char> GetIntegerBuffer(ulong value, int maxChars)
    {
        Span<char> buf = _chars;
        int available = buf.Length - _pos;
        if (available < maxChars && !FitsInteger(value, available))
        {
            Grow(checked(_pos + maxChars));
            buf = _chars;
        }

        return buf;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool FitsInteger(ulong magnitude, int digits) =>
        digits > 0 && magnitude < PowersOfTen[digits];

    // Embedded read-only data: checking a short destination does not allocate or format twice.
    private static ReadOnlySpan<ulong> PowersOfTen =>
    [
        1UL, 10UL, 100UL, 1000UL, 10000UL, 100000UL, 1000000UL, 10000000UL,
        100000000UL, 1000000000UL, 10000000000UL, 100000000000UL,
        1000000000000UL, 10000000000000UL, 100000000000000UL,
        1000000000000000UL, 10000000000000000UL, 100000000000000000UL,
        1000000000000000000UL, 10000000000000000000UL
    ];

    /// <summary>
    /// Appends the string representation of a span-formattable value.
    /// </summary>
    /// <typeparam name="T">The type of the value, must implement <see cref="ISpanFormattable"/>.</typeparam>
    /// <param name="value">The value to format and append.</param>
    /// <param name="format">The format to use.</param>
    /// <param name="provider">The format provider. Can be null for default formatting.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append<T>(T value, scoped ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : ISpanFormattable
    {
        Span<char> buf = _chars;
        if (buf.IsEmpty)
            ThrowIfDisposed();

        if (value.TryFormat(buf.Slice(_pos), out int written, format, provider))
        {
            _pos += written;
            return;
        }

        AppendFormattedSlow(value, format, provider);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AppendFormattedSlow<T>(T value, scoped ReadOnlySpan<char> format, IFormatProvider? provider)
        where T : ISpanFormattable
    {
        if (!format.IsEmpty && format.Overlaps(_chars))
        {
            // Growth returns the old array, so an aliased format must survive independently.
            char[]? rented = null;
            Span<char> copy = format.Length <= 256 ? stackalloc char[format.Length] : (rented = ArrayPool<char>.Shared.Rent(format.Length));
            try
            {
                format.CopyTo(copy);
                AppendFormattedSlow(value, copy.Slice(0, format.Length), provider);
            }
            finally
            {
                if (rented is not null)
                    ArrayPool<char>.Shared.Return(rented);
            }

            return;
        }

        while (true)
        {
            Span<char> buf = _chars;
            int required = _buffer is null ? checked(buf.Length + 1) : (int)Math.Min((long)buf.Length * 2, Array.MaxLength);
            if (required <= buf.Length)
                throw new OutOfMemoryException();
            Grow(required);
            if (value.TryFormat(_chars.Slice(_pos), out int written, format, provider))
            {
                _pos += written;
                return;
            }
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
    public void AppendLine(char c) => Append(c, '\n');

    /// <summary>
    /// Appends a string followed by a newline.
    /// </summary>
    /// <param name="value">The string to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine(string? value) => AppendLine(value.AsSpan());

    /// <summary>
    /// Appends the characters from a span followed by a newline.
    /// </summary>
    /// <param name="value">The span of characters to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine(scoped ReadOnlySpan<char> value)
    {
        Span<char> buf = _chars;
        if (value.Length >= buf.Length - _pos)
        {
            if (buf.IsEmpty)
            {
                Initialize(checked(value.Length + 1));
                buf = _chars;
            }
            else
            {
                GrowAndAppend(value, appendNewline: true);
                return;
            }
        }

        value.CopyTo(buf.Slice(_pos));
        int newPos = _pos + value.Length + 1;
        buf[newPos - 1] = '\n';
        _pos = newPos;
    }

    /// <summary>
    /// Appends the string representation of a span-formattable value followed by a newline.
    /// </summary>
    /// <typeparam name="T">The type of the value, must implement <see cref="ISpanFormattable"/>.</typeparam>
    /// <param name="value">The value to format and append.</param>
    /// <param name="format">The format to use.</param>
    /// <param name="provider">The format provider. Can be null for default formatting.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine<T>(T value, scoped ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GrowAndAppend(scoped ReadOnlySpan<char> value, bool appendNewline)
    {
        int newPos = checked(_pos + value.Length + (appendNewline ? 1 : 0));
        char[] buffer = GrowAndAppendCore(_chars.Slice(0, _pos), value, appendNewline, newPos, _chars.Length, _buffer);
        ReplaceBuffer(buffer);
        _pos = newPos;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static char[] GrowAndAppendCore(ReadOnlySpan<char> contents, ReadOnlySpan<char> value, bool appendNewline, int required, int capacity, char[]? previous)
    {
        char[] buffer = RentBuffer(required, capacity, previous);
        contents.CopyTo(buffer);
        value.CopyTo(buffer.AsSpan(contents.Length));
        if (appendNewline)
            buffer[required - 1] = '\n';
        return buffer;
    }
}

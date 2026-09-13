using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Soenneker.Utils.PooledStringBuilders;

public ref partial struct PooledStringBuilder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InsertCore(int index, scoped ReadOnlySpan<char> value)
    {
        InsertCore(_chars, _pos, index, value);
        _pos += value.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InsertCore(Span<char> chars, int position, int index, ReadOnlySpan<char> value)
    {
        chars.Slice(index, position - index).CopyTo(chars.Slice(index + value.Length));
        value.CopyTo(chars.Slice(index));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InsertOverlapping(int index, scoped ReadOnlySpan<char> value)
    {
        InsertOverlappingCore(_chars, _pos, index, value);
        _pos += value.Length;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InsertOverlappingCore(Span<char> chars, int position, int index, ReadOnlySpan<char> value)
    {
        // A source inside the written contents can be located again after the tail is shifted.
        ((ReadOnlySpan<char>)chars).Overlaps(value, out int sourceIndex);
        if (sourceIndex >= 0 && sourceIndex <= position - value.Length)
        {
            int length = value.Length;
            chars.Slice(index, position - index).CopyTo(chars.Slice(index + length));
            int prefix = Math.Clamp(index - sourceIndex, 0, length);
            chars.Slice(sourceIndex, prefix).CopyTo(chars.Slice(index));
            chars.Slice(sourceIndex + prefix + length, length - prefix).CopyTo(chars.Slice(index + prefix));
            return;
        }

        // Sources outside the written contents need a snapshot before shifting.
        char[]? rented = null;
        Span<char> copy = value.Length <= 256 ? stackalloc char[value.Length] : (rented = ArrayPool<char>.Shared.Rent(value.Length));
        try
        {
            value.CopyTo(copy);
            InsertCore(chars, position, index, copy.Slice(0, value.Length));
        }
        finally
        {
            if (rented is not null)
                ArrayPool<char>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GrowAndInsert(int index, scoped ReadOnlySpan<char> value)
    {
        int newPos = checked(_pos + value.Length);
        char[] buffer = GrowAndInsertCore(_chars.Slice(0, _pos), index, value, newPos, _chars.Length, _buffer);
        ReplaceBuffer(buffer);
        _pos = newPos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char[] GrowAndInsertCore(ReadOnlySpan<char> contents, int index, ReadOnlySpan<char> value, int required, int capacity, char[]? previous)
    {
        char[] buffer = RentBuffer(required, capacity, previous);
        contents.Slice(0, index).CopyTo(buffer);
        value.CopyTo(buffer.AsSpan(index));
        contents.Slice(index).CopyTo(buffer.AsSpan(index + value.Length));
        return buffer;
    }
}

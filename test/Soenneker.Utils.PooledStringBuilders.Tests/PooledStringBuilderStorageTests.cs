using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using AwesomeAssertions;

namespace Soenneker.Utils.PooledStringBuilders.Tests;

public sealed class PooledStringBuilderStorageTests
{
    [Test]
    public void Format_Aliased_To_Builder_Survives_Pool_Reuse_During_Growth()
    {
        using var sb = new PooledStringBuilder(16);
        sb.Append("D20");
        sb.Append('x', sb.Capacity - sb.Length);
        var formatter = new PoolReusingFormattable();
        sb.Append(formatter, sb.AsSpan()[..3]);
        formatter.LastFormat.Should().Be("D20");
        sb.ToString().Should().Be("D20" + new string('x', 13) + new string('v', 20));
    }

    [Test]
    public void Throwing_Formatter_Leaves_Existing_Contents_Usable_After_Growth()
    {
        Span<char> storage = stackalloc char[8];
        using var sb = new PooledStringBuilder(storage);
        sb.Append("prefix");
        bool threw = false;
        try
        {
            sb.Append(new ThrowingFormattable());
        }
        catch (FormatException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
        sb.ToString().Should().Be("prefix");
        sb.Append("suffix");
        sb.ToString().Should().Be("prefixsuffix");
    }

    private sealed class PoolReusingFormattable : ISpanFormattable
    {
        private int _attempts;
        public string? LastFormat { get; private set; }

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            if (++_attempts > 1)
            {
                char[] reused = ArrayPool<char>.Shared.Rent(16);
                reused.AsSpan().Fill('!');
                LastFormat = format.ToString();
                ArrayPool<char>.Shared.Return(reused);
            }

            charsWritten = 0;
            if (destination.Length < 20)
                return false;
            destination[..20].Fill('v');
            charsWritten = 20;
            return true;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) => throw new InvalidOperationException();
    }

    private sealed class ThrowingFormattable : ISpanFormattable
    {
        private int _attempts;
        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            destination.Fill('?');
            charsWritten = 0;
            if (++_attempts > 1)
                throw new FormatException();
            return false;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) => throw new InvalidOperationException();
    }

    [Test]
    public void Caller_Storage_Is_Used_Without_Being_Owned()
    {
        Span<char> storage = stackalloc char[64];
        storage.Fill('?');
        var sb = new PooledStringBuilder(storage);
        sb.Length.Should().Be(0);
        sb.Capacity.Should().Be(64);
        sb.Append("value=");
        sb.Append(-123);
        sb.Append('!');
        sb.ToString().Should().Be("value=-123!");
        new string(storage[..sb.Length]).Should().Be("value=-123!");
        sb.Clear();
        sb.Append('a', 'b', 'c');
        sb.Insert(1, "XYZ");
        sb.Shrink(1);
        sb.AppendLine('!');
        sb.AppendSpan(2).Fill('z');
        sb.ToStringAndDispose().Should().Be("aXYZb!\nzz");
        sb.Dispose();
        storage[0].Should().Be('a');
    }

    [Test]
    public void Clear_Disposal_Clears_Caller_Storage()
    {
        Span<char> storage = stackalloc char[64];
        storage.Fill('?');
        var sb = new PooledStringBuilder(storage);
        sb.Append("secret");
        sb.ToStringAndDispose(clear: true).Should().Be("secret");
        foreach (char c in storage)
            c.Should().Be('\0');
        sb.Dispose(true);
    }

    [Test]
    public void Caller_Storage_Spills_To_Pool_Without_Returning_Caller_Array()
    {
        char[] storage = new char[7]; // This array is deliberately not a pool bucket size.
        storage.AsSpan().Fill('?');
        var sb = new PooledStringBuilder(storage.AsSpan());
        sb.Append("abcdefg");
        sb.Append(sb.AsSpan());
        sb.AppendLine(sb.AsSpan());
        sb.Insert(3, sb.AsSpan().Slice(4, 9));
        sb.ToStringAndDispose().Should().Be("abcefgabcdefdefgabcdefgabcdefgabcdefg\n");
        new string(storage).Should().Be("abcdefg");
        sb.Dispose();
    }

    [Test]
    public void Every_Small_Capacity_Handles_All_Append_Forms()
    {
        Span<char> storage = stackalloc char[64];
        for (int capacity = 0; capacity <= 64; capacity++)
        {
            using var sb = new PooledStringBuilder(storage[..capacity]);
            sb.Append('a');
            sb.Append('b', 'c');
            sb.Append('d', 'e', 'f');
            sb.Append('x', 2);
            sb.AppendSpan(2).Fill('y');
            sb.Append(-12);
            sb.Append(34u);
            sb.Append(long.MinValue);
            sb.Append(ulong.MaxValue);
            sb.Append(123.45m, "F2", CultureInfo.InvariantCulture);
            sb.AppendLine("end".AsSpan());
            sb.ToString().Should().Be("abcdefxxyy-1234-922337203685477580818446744073709551615123.45end\n");
        }
    }

    [Test]
    public void Self_Insert_Works_At_Every_Overlap_Position_With_And_Without_Growth()
    {
        const string original = "01234567";
        Span<char> storage = stackalloc char[32];
        for (int capacity = original.Length; capacity <= 16; capacity++)
        {
            for (int index = 0; index <= original.Length; index++)
            {
                for (int start = 0; start <= original.Length; start++)
                {
                    for (int length = 0; length <= original.Length - start; length++)
                    {
                        using var sb = new PooledStringBuilder(storage[..capacity]);
                        sb.Append(original);
                        sb.Insert(index, sb.AsSpan().Slice(start, length));
                        sb.ToString().Should().Be(original.Insert(index, original.Substring(start, length)));
                    }
                }
            }
        }
    }

    [Test]
    public void Insert_Can_Read_Caller_Memory_Outside_Written_Contents()
    {
        Span<char> storage = stackalloc char[64];
        const string initial = "abcdefghijklmnopqrstuvwxyz0123456789";
        for (int index = 0; index <= 8; index++)
        {
            for (int start = 0; start < 20; start++)
            {
                initial.CopyTo(storage);
                using var sb = new PooledStringBuilder(storage);
                sb.Append(initial.AsSpan(0, 8));
                sb.Insert(index, storage.Slice(start, 12));
                sb.ToString().Should().Be(initial[..8].Insert(index, initial.Substring(start, 12)));
            }
        }
    }

    [Test]
    public void Large_Overlapping_Insert_Snapshot_Is_Returned_After_Use()
    {
        char[] storage = new char[1024];
        storage.AsSpan().Fill('x');
        using var sb = new PooledStringBuilder(storage);
        sb.Append("prefix");
        sb.Insert(2, storage.AsSpan(16, 300));
        sb.ToString().Should().Be("pr" + new string('x', 300) + "efix");
    }

    [Test]
    public void Disposed_Caller_Builder_Rejects_Every_Operation()
    {
        Span<char> storage = stackalloc char[64];
        for (int operation = 0; operation < 21; operation++)
        {
            var sb = new PooledStringBuilder(storage);
            sb.Append("old");
            sb.Dispose();
            bool threw = false;
            try
            {
                switch (operation)
                {
                    case 0: sb.Append('x'); break;
                    case 1: sb.Append('x', 'y'); break;
                    case 2: sb.Append('x', 'y', 'z'); break;
                    case 3: sb.Append("x"); break;
                    case 4: sb.Append("x".AsSpan()); break;
                    case 5: sb.Append('x', 1); break;
                    case 6: sb.AppendSpan(1); break;
                    case 7: sb.Append(1); break;
                    case 8: sb.Append(1u); break;
                    case 9: sb.Append(1L); break;
                    case 10: sb.Append(1UL); break;
                    case 11: sb.Append(1m); break;
                    case 12: sb.AppendLine(); break;
                    case 13: sb.AppendLine("x"); break;
                    case 14: sb.Insert(0, 'x'); break;
                    case 15: sb.Insert(0, "x"); break;
                    case 16: sb.Clear(); break;
                    case 17: sb.Shrink(1); break;
                    case 18: sb.EnsureCapacity(1); break;
                    case 19: _ = sb.Capacity; break;
                    case 20: sb.AsSpan(); break;
                }
            }
            catch (ObjectDisposedException)
            {
                threw = true;
            }

            threw.Should().BeTrue();
        }
    }

    [Test]
    public void Stack_Construction_And_Formatting_Allocate_No_Managed_Memory()
    {
        BuildOnStack(100);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int result = BuildOnStack(1000);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
        result.Should().BeGreaterThan(0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int BuildOnStack(int iterations)
    {
        Span<char> storage = stackalloc char[128];
        int result = 0;
        for (int i = 0; i < iterations; i++)
        {
            using var sb = new PooledStringBuilder(storage);
            sb.Append("value=");
            sb.Append(i);
            sb.AppendLine();
            sb.Insert(0, '[');
            sb.Append(']');
            result += sb.AsSpan()[1];
        }

        return result;
    }
}

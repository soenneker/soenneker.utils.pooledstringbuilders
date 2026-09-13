using System;
using System.Globalization;
using AwesomeAssertions;

namespace Soenneker.Utils.PooledStringBuilders.Tests;

public sealed class PooledStringBuilderPerformanceTests
{
    [Test]
    public void Integer_Digit_Boundaries_Only_Grow_When_Necessary()
    {
        ulong power = 1;
        for (int exponent = 0; exponent < 20; exponent++)
        {
            foreach (ulong magnitude in new[] { power - 1, power, power + 1 })
            {
                for (int available = 0; available <= 21; available++)
                {
                    VerifyUnsigned(magnitude, available);
                    if (magnitude <= long.MaxValue)
                    {
                        VerifySigned((long)magnitude, available);
                        VerifySigned(-(long)magnitude, available);
                    }
                }
            }

            if (exponent < 19)
                power *= 10;
        }

        for (int available = 0; available <= 21; available++)
        {
            VerifySigned(long.MinValue, available);
            VerifySigned(long.MaxValue, available);
            VerifySigned(int.MinValue, available);
            VerifySigned(int.MaxValue, available);
            VerifyUnsigned(uint.MaxValue, available);
            VerifyUnsigned(ulong.MaxValue, available);
        }
    }

    private static void VerifySigned(long value, int available)
    {
        for (int kind = 0; kind < 2; kind++)
        {
            if (kind == 1 && (value < int.MinValue || value > int.MaxValue))
                continue;

            using var sb = new PooledStringBuilder(64);
            int capacity = sb.Capacity;
            string prefix = new('x', capacity - available);
            sb.Append(prefix);
            if (kind == 0)
                sb.Append(value);
            else
                sb.Append((int)value);

            string formatted = value.ToString(CultureInfo.InvariantCulture);
            sb.ToString().Should().Be(prefix + formatted);
            (sb.Capacity == capacity).Should().Be(formatted.Length <= available);
        }
    }

    private static void VerifyUnsigned(ulong value, int available)
    {
        for (int kind = 0; kind < 2; kind++)
        {
            if (kind == 1 && value > uint.MaxValue)
                continue;

            using var sb = new PooledStringBuilder(64);
            int capacity = sb.Capacity;
            string prefix = new('x', capacity - available);
            sb.Append(prefix);
            if (kind == 0)
                sb.Append(value);
            else
                sb.Append((uint)value);

            string formatted = value.ToString(CultureInfo.InvariantCulture);
            sb.ToString().Should().Be(prefix + formatted);
            (sb.Capacity == capacity).Should().Be(formatted.Length <= available);
        }
    }

    [Test]
    public void Stack_Spans_Can_Be_Appended_From_A_Nested_Scope()
    {
        using var sb = new PooledStringBuilder(16);
        {
            Span<char> scratch = stackalloc char[32];
            scratch.Fill('x');
            sb.Append(scratch);
            sb.AppendLine(scratch);
        }

        sb.ToString().Should().Be(new string('x', 64) + "\n");
    }

    [Test]
    public void Integers_Use_Last_Available_Character_Without_Growing()
    {
        using var sb = new PooledStringBuilder(16);
        int capacity = sb.Capacity;
        for (int kind = 0; kind < 4; kind++)
        {
            sb.Clear();
            sb.Append('x', capacity - 1);
            switch (kind)
            {
                case 0: sb.Append(7); break;
                case 1: sb.Append(7u); break;
                case 2: sb.Append(7L); break;
                case 3: sb.Append(7UL); break;
            }

            sb.Capacity.Should().Be(capacity);
            sb.ToString().Should().Be(new string('x', capacity - 1) + "7");
        }
    }

    [Test]
    public void Integer_Extremes_Grow_And_Remain_Invariant()
    {
        using var sb = new PooledStringBuilder(16);
        CultureInfo previous = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "minus";
        try
        {
            CultureInfo.CurrentCulture = culture;
            sb.Append(int.MinValue);
            sb.Append(',');
            sb.Append(uint.MaxValue);
            sb.Append(',');
            sb.Append(long.MinValue);
            sb.Append(',');
            sb.Append(ulong.MaxValue);
            sb.ToString().Should().Be("-2147483648,4294967295,-9223372036854775808,18446744073709551615");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    public void Generic_Formatting_Uses_All_Available_Space_In_One_Attempt()
    {
        using var sb = new PooledStringBuilder(128);
        sb.Append("prefix:");
        var value = new CountingFormattable(100);

        sb.Append(value, "test", CultureInfo.InvariantCulture);

        value.Attempts.Should().Be(1);
        value.LastDestinationLength.Should().Be(sb.Capacity - 7);
        value.LastFormat.Should().Be("test");
        value.LastProvider.Should().BeSameAs(CultureInfo.InvariantCulture);
        sb.ToString().Should().Be("prefix:" + new string('v', 100));
    }

    [Test]
    public void Generic_Formatting_Only_Grows_When_Output_Does_Not_Fit()
    {
        using var sb = new PooledStringBuilder(16);
        int capacity = sb.Capacity;
        sb.Append('x', capacity - 1);
        var value = new CountingFormattable(1);
        sb.Append(value);

        value.Attempts.Should().Be(1);
        sb.Capacity.Should().Be(capacity);

        value = new CountingFormattable(1000);
        sb.Append(value);
        sb.ToString().Should().Be(new string('x', capacity - 1) + new string('v', 1001));
        (sb.Capacity >= sb.Length).Should().BeTrue();
    }

    [Test]
    public void Empty_Formatting_At_Full_Capacity_Does_Not_Grow()
    {
        using var sb = new PooledStringBuilder(16);
        int capacity = sb.Capacity;
        sb.Append('x', capacity);
        sb.Append(new CountingFormattable(0));
        sb.Capacity.Should().Be(capacity);
        sb.Length.Should().Be(capacity);
    }

    [Test]
    public void Large_Default_Appends_Preserve_Contents()
    {
        using PooledStringBuilder sb = default;
        string value = new('x', 1000);
        sb.Append(value);
        sb.AppendLine(value.AsSpan());
        sb.AppendSpan(3).Fill('z');
        sb.ToString().Should().Be(value + value + "\nzzz");
    }

    [Test]
    public void AppendLine_Handles_Exact_Fit_Growth_And_Empty_Values()
    {
        using var sb = new PooledStringBuilder(16);
        int capacity = sb.Capacity;
        sb.AppendLine(new string('x', capacity - 1));
        sb.Capacity.Should().Be(capacity);
        sb.AppendLine("yz".AsSpan());
        sb.AppendLine((string?)null);
        sb.AppendLine(ReadOnlySpan<char>.Empty);
        sb.AppendLine('!');
        sb.ToString().Should().Be(new string('x', capacity - 1) + "\nyz\n\n\n!\n");
    }

    [Test]
    public void Append_Builder_Span_Across_Growth_Preserves_Source()
    {
        using var sb = new PooledStringBuilder(16);
        sb.Append("0123456789abcdef");
        sb.Append(sb.AsSpan().Slice(2, 10));
        sb.AppendLine(sb.AsSpan());
        sb.ToString().Should().Be("0123456789abcdef23456789ab0123456789abcdef23456789ab\n");
    }

    [Test]
    public void Empty_Default_Reads_Still_Allow_Appending()
    {
        PooledStringBuilder sb = default;
        sb.AsSpan().IsEmpty.Should().BeTrue();
        sb.ToString().Should().BeEmpty();
        sb.Append('a');
        sb.ToStringAndDispose().Should().Be("a");
        sb.Dispose();

        sb = default;
        sb.ToStringAndDispose().Should().BeEmpty();
        sb.Length.Should().Be(0);
    }

    [Test]
    public void Disposed_Reads_And_Optimized_Appends_Still_Throw()
    {
        for (int operation = 0; operation < 8; operation++)
        {
            PooledStringBuilder sb = default;
            sb.Dispose();
            bool threw = false;
            try
            {
                switch (operation)
                {
                    case 0: sb.AsSpan(); break;
                    case 1: sb.ToString(); break;
                    case 2: sb.ToStringAndDispose(); break;
                    case 3: sb.Append((string?)null); break;
                    case 4: sb.AppendLine((string?)null); break;
                    case 5: sb.Append(1); break;
                    case 6: sb.Append(new CountingFormattable(0)); break;
                    case 7: sb.EnsureCapacity(0); break;
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
    public void Overflowing_Appends_Leave_Contents_And_Capacity_Intact()
    {
        using var sb = new PooledStringBuilder(16);
        sb.Append("prefix");
        int capacity = sb.Capacity;
        for (int operation = 0; operation < 2; operation++)
        {
            bool threw = false;
            try
            {
                if (operation == 0)
                    sb.AppendSpan(int.MaxValue);
                else
                    sb.Append('x', int.MaxValue);
            }
            catch (OverflowException)
            {
                threw = true;
            }

            threw.Should().BeTrue();
            sb.ToString().Should().Be("prefix");
            sb.Capacity.Should().Be(capacity);
        }
    }

    [Test]
    public void Negative_Capacity_Is_Rejected_Without_Changing_Contents()
    {
        using var sb = new PooledStringBuilder(16);
        sb.Append("prefix");
        bool threw = false;
        try
        {
            sb.EnsureCapacity(-1);
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
        sb.ToString().Should().Be("prefix");
    }

    private sealed class CountingFormattable(int length) : ISpanFormattable
    {
        public int Attempts { get; private set; }
        public int LastDestinationLength { get; private set; }
        public string? LastFormat { get; private set; }
        public IFormatProvider? LastProvider { get; private set; }

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            Attempts++;
            LastDestinationLength = destination.Length;
            LastFormat = format.ToString();
            LastProvider = provider;
            charsWritten = 0;
            if (destination.Length < length)
            {
                // A formatter may write partial output before reporting failure.
                destination.Fill('?');
                return false;
            }

            destination[..length].Fill('v');
            charsWritten = length;
            return true;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) => throw new InvalidOperationException("Should use TryFormat.");
    }
}

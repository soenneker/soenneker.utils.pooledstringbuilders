using AwesomeAssertions;
using Soenneker.Tests.FixturedUnit;
using System;
using Xunit;

namespace Soenneker.Utils.PooledStringBuilders.Tests;

[Collection("Collection")]
public sealed class PooledStringBuilderTests : FixturedUnitTest
{
    public PooledStringBuilderTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact]
    public void Default()
    {
    }


    [Fact]
    public void Append_Chars_And_String_Should_Produce_Expected_Text()
    {
        var sb = new PooledStringBuilder(8);

        sb.Append('A');
        sb.Append('B', 'C');
        sb.Append(' ');
        sb.Append("xyz");

        var s = sb.ToStringAndDispose();

        s.Should()
            .Be("ABC xyz");
    }

    [Fact]
    public void Append_Span_Should_Copy_Data()
    {
        var sb = new PooledStringBuilder(4);

        sb.Append("Hi");
        sb.Append(" ");
        sb.Append("There".AsSpan());

        var s = sb.ToStringAndDispose();

        s.Should()
            .Be("Hi There");
    }

    [Fact]
    public void AppendSpan_Allows_Direct_Writes()
    {
        var sb = new PooledStringBuilder(2);

        var dest = sb.AppendSpan(5);
        "Hello".AsSpan()
            .CopyTo(dest);

        var s = sb.ToStringAndDispose();
        s.Should()
            .Be("Hello");
    }

    [Fact]
    public void Append_Generic_ISpanFormattable_Int_Should_Use_No_Intermediates()
    {
        var sb = new PooledStringBuilder(4);

        sb.Append(123); // int implements ISpanFormattable
        sb.Append(' ');
        sb.Append(4567);

        var s = sb.ToStringAndDispose();
        s.Should()
            .Be("123 4567");
    }

    [Fact]
    public void Append_Generic_With_FormatProvider_Should_Format_Date()
    {
        var sb = new PooledStringBuilder(16);

        var dt = new DateTime(2024, 12, 25, 0, 0, 0, DateTimeKind.Utc);
        sb.Append(dt, "yyyy-MM-dd".AsSpan(), provider: null);

        var s = sb.ToStringAndDispose();
        s.Should()
            .Be("2024-12-25");
    }

    [Fact]
    public void AppendSeparatorIfNotEmpty_Works_As_Delimiter()
    {
        var sb = new PooledStringBuilder(8);

        sb.Append("first");
        sb.AppendSeparatorIfNotEmpty(',');
        sb.Append("second");
        sb.AppendSeparatorIfNotEmpty(',');
        sb.Append("third");

        var s = sb.ToStringAndDispose();
        s.Should()
            .Be("first,second,third");
    }

    [Fact]
    public void AppendLine_Appends_Newline()
    {
        var sb = new PooledStringBuilder(8);

        sb.Append("row1");
        sb.AppendLine();
        sb.Append("row2");

        var s = sb.ToStringAndDispose();
        s.Should()
            .Be("row1\nrow2");
    }

    [Fact]
    public void EnsureCapacity_Grows_And_Capacity_Remains_PowerOfTwo()
    {
        var sb = new PooledStringBuilder(4);

        // Force growth beyond initial capacity
        sb.Append(new string('x', 1000));

        // Capacity should be >= Length and (implementation detail) a power-of-two
        int length = sb.Length;
        int capacity = sb.Capacity;

        (capacity >= length).Should()
            .BeTrue("capacity must accommodate current length");
        IsPowerOfTwo(capacity)
            .Should()
            .BeTrue("growth rounds to next power-of-two");

        sb.Dispose();
    }

    [Fact]
    public void Clear_Resets_Length_But_Not_Capacity()
    {
        var sb = new PooledStringBuilder(8);

        sb.Append("ABCDEFGHIJ"); // will grow
        int capacityBefore = sb.Capacity;

        sb.Clear();
        sb.Length.Should()
            .Be(0);
        sb.Capacity.Should()
            .Be(capacityBefore, "Clear should not shrink the buffer");

        sb.Dispose();
    }

    [Fact]
    public void ToStringAndDispose_Returns_String_And_Releases_Buffer()
    {
        var sb = new PooledStringBuilder(4);
        sb.Append("done");

        var s = sb.ToStringAndDispose();
        s.Should()
            .Be("done");

        // Dispose again is safe (idempotent behavior)
        sb.Dispose();
    }

    [Fact]
    public void Append_Null_Or_Empty_String_Should_Be_NoOp()
    {
        var sb = new PooledStringBuilder(8);
        sb.Append((string?)null);
        sb.Append(string.Empty);

        sb.Length.Should()
            .Be(0);

        sb.Append("x");
        sb.Length.Should()
            .Be(1);

        sb.Dispose();
    }

    private static bool IsPowerOfTwo(int x)
    {
        // 0 or negative is not power-of-two
        if (x <= 0) return false;
        // x & (x-1) == 0 for powers of two
        return (x & (x - 1)) == 0;
    }
}
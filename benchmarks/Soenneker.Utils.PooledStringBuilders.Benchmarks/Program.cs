using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Soenneker.Utils.PooledStringBuilders;
using System.Globalization;
using System.Runtime.CompilerServices;

BenchmarkSwitcher.FromAssembly(typeof(CharacterBenchmarks).Assembly).Run(args);

[MemoryDiagnoser]
public class CharacterBenchmarks
{
    [Params(16, 128, 1024)]
    public int Count { get; set; }

    [Benchmark(Baseline = true)]
    public int Rented()
    {
        using var builder = new PooledStringBuilder(128);
        for (int i = 0; i < Count; i++)
            builder.Append((char)i);
        return Consume(builder.AsSpan());
    }

    [Benchmark]
    public int StackThenPool()
    {
        Span<char> initial = stackalloc char[128];
        using var builder = new PooledStringBuilder(initial);
        for (int i = 0; i < Count; i++)
            builder.Append((char)i);
        return Consume(builder.AsSpan());
    }

    [Benchmark]
    public int DirectSpanControl()
    {
        // A control for known-length output, without growth or disposal semantics.
        Span<char> destination = stackalloc char[Count];
        for (int i = 0; i < Count; i++)
            destination[i] = (char)i;
        return Consume(destination);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int Consume(ReadOnlySpan<char> contents) => contents[0] ^ contents[contents.Length / 2] ^ contents[^1] ^ contents.Length;
}

[MemoryDiagnoser]
public class FormattingBenchmarks
{
    [Params(7, 123456789, int.MinValue)]
    public int Value { get; set; }

    [Benchmark(Baseline = true)]
    public int Rented()
    {
        using var builder = new PooledStringBuilder(16);
        builder.Append(Value);
        return CharacterBenchmarks.Consume(builder.AsSpan());
    }

    [Benchmark]
    public int Stack()
    {
        Span<char> storage = stackalloc char[16];
        using var builder = new PooledStringBuilder(storage);
        builder.Append(Value);
        return CharacterBenchmarks.Consume(builder.AsSpan());
    }

    [Benchmark]
    public int DirectTryFormatControl()
    {
        Span<char> storage = stackalloc char[16];
        Value.TryFormat(storage, out int written, provider: CultureInfo.InvariantCulture);
        return CharacterBenchmarks.Consume(storage[..written]);
    }
}

[MemoryDiagnoser]
public class StringResultBenchmarks
{
    private const string Prefix = "item=";
    private const string Suffix = ";status=active\n";
    public int Value { get; set; } = 123456789;

    [Benchmark(Baseline = true)]
    public string Rented()
    {
        var builder = new PooledStringBuilder(128);
        builder.Append(Prefix);
        builder.Append(Value);
        builder.Append(Suffix);
        return builder.ToStringAndDispose();
    }

    [Benchmark]
    public string Stack()
    {
        Span<char> storage = stackalloc char[64];
        var builder = new PooledStringBuilder(storage);
        builder.Append(Prefix);
        builder.Append(Value);
        builder.Append(Suffix);
        return builder.ToStringAndDispose();
    }

    [Benchmark]
    public string KnownLengthStringCreateControl() => string.Create(Prefix.Length + 9 + Suffix.Length, Value, static (destination, value) =>
    {
        Prefix.CopyTo(destination);
        value.TryFormat(destination[Prefix.Length..], out int written, provider: CultureInfo.InvariantCulture);
        Suffix.CopyTo(destination[(Prefix.Length + written)..]);
    });
}

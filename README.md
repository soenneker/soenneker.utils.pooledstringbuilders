[![](https://img.shields.io/nuget/v/soenneker.utils.pooledstringbuilders.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.pooledstringbuilders/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.pooledstringbuilders/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.utils.pooledstringbuilders/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.utils.pooledstringbuilders.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.pooledstringbuilders/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.pooledstringbuilders/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.utils.pooledstringbuilders/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Utils.PooledStringBuilders

**Tiny, fast `ref struct` string builder.**
Uses caller-provided storage or `ArrayPool<char>`. Low allocations. Short-lived use.

## Installation

```bash
dotnet add package Soenneker.Utils.PooledStringBuilders
```

## Example

```csharp
using Soenneker.Utils.PooledStringBuilders;

using var sb = new PooledStringBuilder(128);

sb.Append("Hello, ");
sb.Append(name);
sb.Append(' ');
sb.Append(id);        // ISpanFormattable path, no boxing
sb.AppendLine();

string s = sb.ToString(); // creates a string; using returns the buffer afterward
```

Use `ToString()` when the builder remains in scope and will be disposed separately. For a one-shot
finish without a `using` declaration:

```csharp
var sb = new PooledStringBuilder();
sb.Append("value=");
sb.Append(value);

string result = sb.ToStringAndDispose();
```

## Cheatsheet

- `new PooledStringBuilder(int capacity = 128)`
- `new PooledStringBuilder(Span<char> initialBuffer)` — use stack or caller-owned memory until growth is needed
- `Append(char)`, `Append(string?)`, `Append(ReadOnlySpan<char>)`
- `Append<T>(T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)` where `T : ISpanFormattable`
- `AppendSpan(int length)` — reserve and write directly into the buffer
- `Insert(...)`, `Shrink(int)`, `AppendLine(...)`, `AppendSeparatorIfNotEmpty(char)`
- `Length`, `Capacity`, `AsSpan()`, `EnsureCapacity(int)`, `Clear()`
- `ToString()` — create a string without disposing the builder
- `ToStringAndDispose(bool clear = false)` — create a string and return the buffer
- `Dispose()` / `Dispose(bool clear)`

## Notes

- `PooledStringBuilder` is a stack-only `ref struct`; it cannot be boxed, captured, stored in a normal field, or kept across `await`.
- Do not copy the builder (`var copy = builder`). Copies refer to the same rented array and can return it to the pool more than once. Pass it by `ref` when a helper must mutate the same builder.
- Dispose exactly once, either through `using`, `Dispose`, or `ToStringAndDispose`. Do not use `ToStringAndDispose` and then dispose a copied or aliased value.
- `AppendSpan(length)` immediately increases `Length` and returns uninitialized pooled storage. Fill the entire span before reading or converting the builder, or previous pool contents could appear in the result.
- `AsSpan()` is valid only until the builder grows, changes, or is disposed. Do not retain it.
- `AppendLine` appends `\n`, not `Environment.NewLine`.
- Reading an empty default builder with `AsSpan()`, `ToString()`, or `ToStringAndDispose()` does not rent a buffer. A large first append rents its required capacity directly.
- Integer appends reuse the remaining space when the value fits, even if the type's maximum width would not fit. Generic formatting uses all remaining buffer space before growing.
- `Append(ReadOnlySpan<char>)` and `AppendLine(ReadOnlySpan<char>)` accept scoped spans, including temporary stack-allocated buffers, and copy their contents immediately.
- `Clear()` resets the logical length without zeroing storage. `Dispose(clear: true)` and `ToStringAndDispose(clear: true)` clear the current storage, including caller-provided memory if it is still in use. Earlier storage released during growth is not cleared. The returned managed string is immutable.
- The builder is not thread-safe. Keep it short-lived and confined to one synchronous scope.

## Avoiding buffer rentals

For small results with a predictable upper bound, provide a modest stack buffer:

```csharp
Span<char> initialBuffer = stackalloc char[128];
using var sb = new PooledStringBuilder(initialBuffer);
sb.Append("item=");
sb.Append(id);

ReadOnlySpan<char> contents = sb.AsSpan(); // consume before the builder is modified or disposed
```

This path does not rent or allocate a character buffer when the contents fit. If the contents outgrow
the supplied storage, the builder copies them into a pooled array. `ToString()` still creates the final
nonempty string; consume `AsSpan()` when a string is unnecessary. The initial storage must remain valid
for the builder's lifetime, and the caller retains ownership of it. Avoid large or repeated stack
allocations inside loops; allocate a modest initial buffer outside the loop and reuse it.

## Benchmarks

The [benchmark project](benchmarks/Soenneker.Utils.PooledStringBuilders.Benchmarks) compares rented and
stack-backed construction with direct span, `TryFormat`, and known-length `string.Create` controls.
Those controls have fewer responsibilities than a general-purpose builder and show the remaining overhead.

```bash
dotnet run --project benchmarks/Soenneker.Utils.PooledStringBuilders.Benchmarks -c Release -f net10.0 -- --filter "*" --runtimes net10.0
```

Use `net8.0` or `net9.0` for the other supported runtimes. Compare results on the same runtime and machine;
buffer sizes, formatting distributions, growth, and whether the result needs to be a string all affect performance.

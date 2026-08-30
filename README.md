[![](https://img.shields.io/nuget/v/soenneker.utils.pooledstringbuilders.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.pooledstringbuilders/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.pooledstringbuilders/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.utils.pooledstringbuilders/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.utils.pooledstringbuilders.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.pooledstringbuilders/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.pooledstringbuilders/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.utils.pooledstringbuilders/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Utils.PooledStringBuilders

**Tiny, fast `ref struct` string builder.**
Backed by `ArrayPool<char>`. Low allocations. Short-lived use.

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
- `Clear()` resets the logical length but does not zero the array. Use `Dispose(clear: true)` or `ToStringAndDispose(clear: true)` when the pooled buffer contained secrets. The returned managed string is still immutable and cannot be securely erased.
- The builder is not thread-safe. Keep it short-lived and confined to one synchronous scope.

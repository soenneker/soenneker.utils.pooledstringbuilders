[![](https://img.shields.io/nuget/v/soenneker.utils.pooledstringbuilders.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.pooledstringbuilders/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.pooledstringbuilders/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.utils.pooledstringbuilders/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.utils.pooledstringbuilders.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.pooledstringbuilders/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Utils.PooledStringBuilders

**Tiny, fast `ref struct` string builder.**
Backed by `ArrayPool<char>`. Low allocations. Short-lived use.

## Install

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

string s = sb.ToString(); // returns string + returns buffer
```

## Cheatsheet

* `new PooledStringBuilder(int capacity = 128)`
* `Append(char)`, `Append(string?)`, `Append(ReadOnlySpan<char>)`
* `Append<T>(T value, ReadOnlySpan<char> fmt = default, IFormatProvider? prov = null)` where `T : ISpanFormattable`
* `AppendSpan(int length)` → write directly into the buffer
* `AppendLine()`, `AppendSeparatorIfNotEmpty(char)`
* `Length`, `Capacity`, `Clear()`
* `ToString()` (keep using; you must `Dispose()` later)
* `ToStringAndDispose(bool clear = false)` (one-shot finish)
* `Dispose()` / `Dispose(bool clear)`

## Notes

* **`ref struct`** → stack-only. Don’t capture, box, store in fields, or cross `await`.
* **Dispose when done.** `using` should be used, or there is `ToStringAndDispose()`. Don't use both.
* **Handling secrets?** Use `ToStringAndDispose(clear: true)` to zero the array before returning to the pool.
* Not thread-safe. Keep it short-lived and single-scope.
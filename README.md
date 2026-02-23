# PinnedMemory

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![nuget](https://img.shields.io/nuget/v/PinnedMemory.svg)](https://www.nuget.org/packages/PinnedMemory/)

`PinnedMemory` is a lightweight .NET library for handling sensitive or performance-critical arrays that should stay pinned in memory, be zeroed, and optionally be memory-locked at the OS level.

It is designed for scenarios such as:

- handling secrets in byte/char buffers,
- passing stable pointers to native interop,
- reducing GC relocation concerns for low-level memory operations.

---

## Table of Contents

- [Why use PinnedMemory?](#why-use-pinnedmemory)
- [Install](#install)
- [Supported platforms and types](#supported-platforms-and-types)
- [Quick start](#quick-start)
- [API behavior at a glance](#api-behavior-at-a-glance)
- [Usage patterns](#usage-patterns)
- [Security and correctness best practices](#security-and-correctness-best-practices)
- [Performance notes](#performance-notes)
- [Common pitfalls](#common-pitfalls)
- [FAQ](#faq)
- [License](#license)

---

## Why use PinnedMemory?

When managed arrays are not pinned, the GC may move them. `PinnedMemory<T>` pins an array for the lifetime of the object and can also:

- **zero** the buffer,
- **lock** memory pages (platform permitting),
- **zero + unlock on dispose**.

This gives you a safer lifecycle for sensitive data and a predictable address for native interop work.

---

## Install

### .NET CLI

```bash
dotnet add package PinnedMemory
```

### Package Manager Console

```powershell
Install-Package PinnedMemory
```

### NuGet

- https://www.nuget.org/packages/PinnedMemory/

---

## Supported platforms and types

### Platforms

- Windows
- Linux
- macOS
- Android
- iOS

### Supported `T` element types

- `sbyte`
- `byte`
- `char`
- `short`
- `ushort`
- `int`
- `uint`
- `long`
- `ulong`
- `float`
- `double`
- `decimal`
- `bool`

If you use an unsupported `struct`, construction throws `ArrayTypeMismatchException`.

---

## Quick start

```csharp
using PinnedMemory;

var bytes = new byte[32];

using var secret = new PinnedMemory<byte>(bytes);

secret[0] = 0x41;
secret.Write(1, 0x42);

var first = secret.Read(0);
var copy = secret.ToArray();
```

> By default, construction uses `zero: true` and `locked: true`.

---

## API behavior at a glance

### Constructor

```csharp
PinnedMemory(T[] value, bool zero = true, bool locked = true, SystemType type = SystemType.Unknown)
```

- `value`: source array copied into an internal pooled buffer.
- `zero`:
  - `true` (default): internal memory is zeroed immediately after allocation.
  - `false`: initial contents are preserved.
- `locked`:
  - `true` (default): attempts OS-level page lock.
  - `false`: skip lock attempt.
- `type`: optional explicit OS selection (`Windows`, `Linux`, `Osx`, `Android`, `Ios`); when `Unknown`, OS is detected automatically at runtime.

### Important lifecycle semantics

- Construction rents an internal buffer from `ArrayPool<T>.Shared`.
- `Dispose()`:
  - zeroes internal memory,
  - unlocks if locking was requested,
  - frees the pin handle,
  - returns the buffer to the pool.

### Members

- `Length`: logical length from the original input array.
- Indexer `this[int i]`: get/set values.
- `Read()`: returns internal buffer reference.
- `Read(int index)`: read one value.
- `Write(int index, T value)`: write one value.
- `ToArray()`: returns internal buffer reference.
- `Clone()`: deep-copies into a new `PinnedMemory<T>`.

---

## Usage patterns

### 1) Preserve initial bytes on creation

Use `zero: false` when you provide meaningful input data.

```csharp
using var pin = new PinnedMemory<byte>(new byte[] { 0x10, 0x20, 0x30 }, zero: false);
```

### 2) Build value incrementally

Keep default `zero: true` and fill explicitly.

```csharp
using var pin = new PinnedMemory<byte>(new byte[3]);
pin[0] = 65;
pin[1] = 61;
pin[2] = 77;
```

### 3) Clone for isolated lifecycle

```csharp
using var original = new PinnedMemory<byte>(new byte[] { 1, 2, 3 }, zero: false);
using var clone = original.Clone();
```

### 4) `char[]` for sensitive text-like data

```csharp
using var text = new PinnedMemory<char>(new[] { 's', 'e', 'c', 'r', 'e', 't' }, zero: false);
```

Prefer `char[]`/`byte[]` over `string` when handling secrets.

---

## Security and correctness best practices

1. **Always dispose promptly**
   - Use `using` / `using var`.
   - Do not rely on process shutdown for cleanup.

2. **Choose `zero` intentionally**
   - `zero: true` for allocate-then-populate flows.
   - `zero: false` for pre-populated input arrays.

3. **Avoid converting secrets to `string`**
   - `string` is immutable and not controllably zeroable.
   - Keep secrets in pinned `byte[]`/`char[]` as long as possible.

4. **Minimize copies**
   - `Read()`/`ToArray()` expose the internal buffer reference.
   - If you copy data elsewhere, you now have additional memory to scrub.

5. **Treat locking as best effort**
   - OS lock calls can be constrained by permissions/limits.
   - Keep least-privilege defaults in deployment (e.g., memlock limits on Linux).

6. **Do not access after dispose**
   - The underlying buffer is returned to `ArrayPool<T>`.
   - Retained references can observe reused data from other code.

7. **Keep scope small**
   - Hold pinned memory only for the shortest practical duration.
   - Long-lived pinned regions can negatively affect GC behavior.

---

## Performance notes

- The library uses `ArrayPool<T>.Shared` to reduce allocation pressure.
- Pinning and OS page-locking have costs; use only where justified.
- For small, frequent operations, benchmark your real workload with and without locking.

---

## Common pitfalls

- **“My provided bytes are all zero.”**
  - You likely used default `zero: true`.
  - Set `zero: false` when preserving initial values.

- **“I used `ToArray()` then disposed, but still read old reference.”**
  - `ToArray()` returns internal buffer reference, not a defensive copy.
  - Do not keep references past disposal.

- **“Can I call `ToString()` to inspect data?”**
  - No. `ToString()` intentionally throws `SecurityException`.

---

## FAQ

### Is this a replacement for cryptographic key vaulting/HSMs?

No. It helps memory hygiene inside your process. It is not a full secret-management system.

### Is memory lock guaranteed?

No. Locking is platform- and permission-dependent.

### Is `PinnedMemory<T>` thread-safe?

No explicit thread-safety guarantees are provided. Synchronize concurrent access at the caller boundary.

---

## License

This project is licensed under the [MIT License](https://opensource.org/licenses/MIT).

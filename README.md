
# PinnedMemory

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![nuget](https://img.shields.io/nuget/v/PinnedMemory.svg)](https://www.nuget.org/packages/PinnedMemory/)

**PinnedMemory** is a cross-platform, high-performance solution for creating, accessing, and managing pinned and locked memory for Windows, macOS, and Linux operating systems in .NET Core. It provides automatic memory pinning and optional locking for sensitive operations, helping you prevent garbage collection relocations and offering enhanced performance in low-level memory manipulation scenarios.

# Install

From a command prompt:

```bash
dotnet add package PinnedMemory
```

```bash
Install-Package PinnedMemory
```

You can also search for the package via your NuGet UI or website:

[NuGet: PinnedMemory](https://www.nuget.org/packages/PinnedMemory/)

# Features

- Cross-platform memory management for Windows, Linux, and macOS.
- Supports several primitive types (e.g., `byte`, `int`, `float`, etc.).
- Offers zeroing, locking, and unlocking of memory for security and performance.
- Prevents garbage collection from relocating memory by pinning the array in memory.
- Efficient cloning and pooling of arrays using `ArrayPool<T>`.
- Optimized for performance with aggressive inlining and reduced allocations.

# Usage

### Basic Example

```csharp
using (var pin = new PinnedMemory<byte>(new byte[3]))
{
    pin[0] = 65;
    pin[1] = 61;
    pin[2] = 77;
}
```

### Writing to Memory

```csharp
using (var pin = new PinnedMemory<byte>(new byte[3]))
{
    pin.Write(0, 65);
    pin.Write(1, 61);
    pin.Write(2, 77);
}
```

### Reading from Memory

```csharp
using (var pin = new PinnedMemory<byte>(new byte[] {65, 61, 77}, false))
{
    var byte1 = pin[0];
    var byte2 = pin[1];
    var byte3 = pin[2];
}
```

```csharp
using (var pin = new PinnedMemory<byte>(new byte[] {65, 61, 77}, false))
{
    var byte1 = pin.Read(0);
    var byte2 = pin.Read(1);
    var byte3 = pin.Read(2);
}
```

### Cloning Pinned Memory

```csharp
using (var pin = new PinnedMemory<byte>(new byte[] {65, 61, 77}, false))
{
    var clone = pin.Clone();
    var clonedArray = clone.ToArray();
}
```

### Zeroing, Locking, and Unlocking Memory

```csharp
using (var pin = new PinnedMemory<byte>(new byte[3], zero: true, locked: true))
{
    // Memory is automatically zeroed and locked.
    // Perform secure operations.
}
```

### Supported Types

The following primitive types are supported in `PinnedMemory`:

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

# API Reference

### Constructor

```csharp
PinnedMemory(T[] value, bool zero = true, bool locked = true, SystemType type = SystemType.Unknown)
```

- **value**: The array to pin in memory.
- **zero**: Optional. If `true`, the memory will be zeroed out after allocation.
- **locked**: Optional. If `true`, the memory will be locked in RAM to prevent paging.
- **type**: Optional. Specifies the OS platform (`SystemType.Windows`, `SystemType.Linux`, `SystemType.Osx`). If `Unknown`, it is detected automatically.

### Properties

- **T this[int i]**: Indexer for accessing elements in the pinned array.
- **int Length**: Returns the length of the pinned array.

### Methods

- **T[] Read()**: Returns the entire pinned array.
- **T Read(int index)**: Reads the value at the specified index.
- **void Write(int index, T value)**: Writes the value at the specified index.
- **PinnedMemory<T> Clone()**: Clones the pinned memory array and returns a new `PinnedMemory<T>` object.
- **T[] ToArray()**: Returns a copy of the pinned memory as an array.
- **void Dispose()**: Frees the pinned memory, zeroes it out, and unlocks it if locked.

# License

This library is licensed under the [MIT License](https://opensource.org/licenses/MIT).

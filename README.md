# PinnedMemory

PinnedMemory is a cross platform method for creating, and accessing pinned, and locked memory for Windows, Mac, and Linux operating systems in .NET Core.

# Install

From a command prompt
```bash
dotnet add package PinnedMemory
```

```bash
Install-Package PinnedMemory
```

You can also search for package via your nuget ui / website:

https://www.nuget.org/packages/PinnedMemory/

# Examples

You can find more examples in the github examples project.

```csharp
using (var pin = new PinnedMemory<byte>(new byte[3]))
{
  pin[0] = 65;
  pin[1] = 61;
  pin[2] = 77;
}
```

```csharp
using (var pin = new PinnedMemory<byte>(new byte[3]))
{
  pin.Write(0, 65);
  pin.Write(0, 61);
  pin.Write(0, 77);
}
```

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

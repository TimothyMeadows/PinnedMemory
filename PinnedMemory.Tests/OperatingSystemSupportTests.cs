using System;
using System.Runtime.InteropServices;
using Xunit;

namespace PinnedMemory.Tests;

public class OperatingSystemSupportTests
{
    [Theory]
    [InlineData(SystemType.Windows, typeof(WindowsMemory))]
    [InlineData(SystemType.Linux, typeof(LinuxMemory))]
    [InlineData(SystemType.Osx, typeof(OsxMemory))]
    [InlineData(SystemType.Android, typeof(AndroidMemory))]
    [InlineData(SystemType.Ios, typeof(IosMemory))]
    public void CreateMemory_ReturnsExpectedMemoryImplementation(SystemType type, Type expectedMemoryType)
    {
        var memory = PinnedMemory<byte>.CreateMemory(type, _ => false);

        Assert.IsType(expectedMemoryType, memory);
    }

    [Theory]
    [InlineData(SystemType.Windows, "WINDOWS")]
    [InlineData(SystemType.Linux, "LINUX")]
    [InlineData(SystemType.Osx, "OSX")]
    [InlineData(SystemType.Android, "ANDROID")]
    [InlineData(SystemType.Ios, "IOS")]
    public void ResolveSystemType_DetectsExpectedPlatform(SystemType expectedType, string detectedPlatform)
    {
        var actual = PinnedMemory<byte>.ResolveSystemType(
            SystemType.Unknown,
            platform => platform.Equals(OSPlatform.Create(detectedPlatform)));

        Assert.Equal(expectedType, actual);
    }

    [Fact]
    public void ResolveSystemType_ReturnsUnknown_WhenNoPlatformMatches()
    {
        var actual = PinnedMemory<byte>.ResolveSystemType(SystemType.Unknown, _ => false);

        Assert.Equal(SystemType.Unknown, actual);
    }

    [Fact]
    public void Constructor_UsesExplicitAndroidTypeWithoutRuntimeDetection()
    {
        using var buffer = new PinnedMemory<byte>(new byte[] { 1, 2, 3 }, zero: false, locked: false, type: SystemType.Android);

        Assert.Equal(3, buffer.Length);
        Assert.Equal((byte)1, buffer[0]);
    }

    [Fact]
    public void Constructor_UsesExplicitIosTypeWithoutRuntimeDetection()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        using var buffer = new PinnedMemory<byte>(new byte[] { 4, 5, 6 }, zero: false, locked: false, type: SystemType.Ios);

        Assert.Equal(3, buffer.Length);
        Assert.Equal((byte)4, buffer[0]);
    }
}

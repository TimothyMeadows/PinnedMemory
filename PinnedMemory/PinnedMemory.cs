using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace PinnedMemory;

public sealed class PinnedMemory<T> : IDisposable where T : struct
{
    private static readonly OSPlatform AndroidPlatform = OSPlatform.Create("ANDROID");
    private static readonly OSPlatform IosPlatform = OSPlatform.Create("IOS");

    private static readonly IReadOnlyDictionary<Type, int> s_types = new Dictionary<Type, int>
    {
        { typeof(sbyte), sizeof(sbyte) },
        { typeof(byte), sizeof(byte) },
        { typeof(char), sizeof(char) },
        { typeof(short), sizeof(short) },
        { typeof(ushort), sizeof(ushort) },
        { typeof(int), sizeof(int) },
        { typeof(uint), sizeof(uint) },
        { typeof(long), sizeof(long) },
        { typeof(ulong), sizeof(ulong) },
        { typeof(float), sizeof(float) },
        { typeof(double), sizeof(double) },
        { typeof(decimal), sizeof(decimal) },
        { typeof(bool), sizeof(bool) }
    };

    public static IReadOnlyDictionary<Type, int> Types => s_types;

    private static readonly ArrayPool<T> Pool = ArrayPool<T>.Shared;

    private GCHandle _handle;
    private readonly T[] _buffer;
    private readonly int _size;
    private readonly bool _locked;
    private readonly MemoryBase _memory;
    private bool _disposed;

    public T this[int i]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return _buffer[i];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            ThrowIfDisposed();
            _buffer[i] = value;
        }
    }

    public int Length { get; }

    public PinnedMemory(T[] value, bool zero = true, bool locked = true, SystemType type = SystemType.Unknown)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!Types.TryGetValue(typeof(T), out _size))
        {
            throw new ArrayTypeMismatchException(
                $"{nameof(T)} is not a supported pinned memory type. Supported types are: {string.Join(", ", Types.Keys.Select(t => t.Name))}");
        }

        _buffer = Pool.Rent(value.Length);
        Array.Copy(value, _buffer, value.Length);
        Length = value.Length;
        _locked = locked;
        _memory = CreateMemory(type, RuntimeInformation.IsOSPlatform);

        _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        var pointer = _handle.AddrOfPinnedObject();
        var context = new UIntPtr((uint)(_size * Length));

        if (zero)
            _memory.Zero(pointer, context);
        if (_locked)
            _memory.Lock(pointer, context);
    }

    internal static SystemType ResolveSystemType(SystemType type, Func<OSPlatform, bool> isOsPlatform)
    {
        if (type != SystemType.Unknown)
            return type;

        if (isOsPlatform(OSPlatform.Windows))
            return SystemType.Windows;
        if (isOsPlatform(OSPlatform.Linux))
            return SystemType.Linux;
        if (isOsPlatform(OSPlatform.OSX))
            return SystemType.Osx;
        if (isOsPlatform(AndroidPlatform))
            return SystemType.Android;
        if (isOsPlatform(IosPlatform))
            return SystemType.Ios;

        return SystemType.Unknown;
    }

    internal static MemoryBase CreateMemory(SystemType type, Func<OSPlatform, bool> isOsPlatform)
    {
        var resolvedType = ResolveSystemType(type, isOsPlatform);
        return resolvedType switch
        {
            SystemType.Windows => new WindowsMemory(),
            SystemType.Linux => new LinuxMemory(),
            SystemType.Osx => new OsxMemory(),
            SystemType.Android => new AndroidMemory(),
            SystemType.Ios => new IosMemory(),
            _ => throw new NotSupportedException("No pinned memory support for current operating system")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T[] Read()
    {
        ThrowIfDisposed();
        return _buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Read(int index)
    {
        ThrowIfDisposed();
        return _buffer[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(int index, T value)
    {
        ThrowIfDisposed();
        _buffer[index] = value;
    }

    public PinnedMemory<T> Clone()
    {
        ThrowIfDisposed();

        var buffer = new T[Length];
        if (typeof(T).IsPrimitive)
            Buffer.BlockCopy(_buffer, 0, buffer, 0, _buffer.Length * _size);
        else
            Array.Copy(_buffer, buffer, _buffer.Length);

        return new PinnedMemory<T>(buffer, false);
    }

    public T[] ToArray()
    {
        ThrowIfDisposed();
        return _buffer;
    }

    public override string ToString() => throw new SecurityException("Can't be expressed as String.");

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    ~PinnedMemory()
    {
        Dispose(disposing: false);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (_handle.IsAllocated)
        {
            try
            {
                var pointer = _handle.AddrOfPinnedObject();
                var context = new UIntPtr((uint)(_size * Length));

                _memory.Zero(pointer, context);
                if (_locked)
                    _memory.Unlock(pointer, context);
            }
            finally
            {
                _handle.Free();
                Pool.Return(_buffer, clearArray: true);
            }
        }

        _disposed = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

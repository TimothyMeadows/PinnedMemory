using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Security;
using System.Buffers;

namespace PinnedMemory
{
    public class PinnedMemory<T> : IDisposable where T : struct
    {
        public static Dictionary<Type, int> Types =>
            new Dictionary<Type, int>
            {
                {typeof(sbyte), sizeof(sbyte)},
                {typeof(byte), sizeof(byte)},
                {typeof(char), sizeof(char)},
                {typeof(short), sizeof(short)},
                {typeof(ushort), sizeof(ushort)},
                {typeof(int), sizeof(int)},
                {typeof(uint), sizeof(uint)},
                {typeof(long), sizeof(long)},
                {typeof(ulong), sizeof(ulong)},
                {typeof(float), sizeof(float)},
                {typeof(double), sizeof(double)},
                {typeof(decimal), sizeof(decimal)},
                {typeof(bool), sizeof(bool)}
            };

        private GCHandle _handle;
        private readonly T[] _buffer;
        private readonly int _size;
        private readonly bool _locked;
        private readonly MemoryBase _memory;
        private readonly object _lock = new object();
        private static readonly ArrayPool<T> _pool = ArrayPool<T>.Shared;
        private bool _disposed = false;

        public T this[int i]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer[i];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _buffer[i] = value;
        }

        public int Length { get; }

        public PinnedMemory(T[] value, bool zero = true, bool locked = true, SystemType type = SystemType.Unknown)
        {
            if (!Types.TryGetValue(typeof(T), out _size))
                throw new ArrayTypeMismatchException(
                    $"{nameof(T)} is not a supported pinned memory type. Supported types are: {string.Join(", ", Types.Keys.Select(t => t.Name))}");

            _buffer = _pool.Rent(value.Length);
            Array.Copy(value, _buffer, value.Length);
            Length = value.Length;
            _locked = locked;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _memory = new WindowsMemory();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _memory = new LinuxMemory();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _memory = new OsxMemory();
            }
            else
            {
                throw new NotSupportedException("No pinned memory support for current operating system");
            }

            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            var pointer = _handle.AddrOfPinnedObject();
            var context = new UIntPtr((uint)(_size * Length));

            if (zero)
                _memory.Zero(pointer, context);
            if (_locked)
                _memory.Lock(pointer, context);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] Read()
        {
            return _buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read(int index)
        {
            return _buffer[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(int index, T value)
        {
            _buffer[index] = value;
        }

        public PinnedMemory<T> Clone()
        {
            var buffer = new T[Length];
            if (typeof(T).IsPrimitive)
                Buffer.BlockCopy(_buffer, 0, buffer, 0, _buffer.Length * _size);
            else
                Array.Copy(_buffer, buffer, _buffer.Length);

            return new PinnedMemory<T>(buffer, false);
        }

        public T[] ToArray()
        {
            return _buffer;
        }

        public override string ToString()
        {
            throw new SecurityException("Can't be expressed as String.");
        }

        public void Dispose()
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
                    _pool.Return(_buffer);
                }
            }
            _disposed = true;
        }
    }
}

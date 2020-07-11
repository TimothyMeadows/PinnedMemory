/*
 * Based on original idea found here:
 * https://stackoverflow.com/questions/1166952/net-secure-memory-structures/38552838#38552838
 * Linux, and OSX improvements implemented from here:
 * https://github.com/mheyman/Isopoh.Cryptography.Argon2/tree/master/Isopoh.Cryptography.SecureArray
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;

namespace PinnedMemory
{
    public class PinnedMemory<T> : IDisposable where T : struct
    {
        public static Dictionary<Type, int> Types =>
            new Dictionary<Type, int>
            {
                {typeof(sbyte), sizeof(sbyte)},
                {typeof(byte), sizeof(byte)},
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

        public T this[int i]
        {
            get => _buffer[i];
            set => _buffer[i] = value;
        }

        public int Length { get; }

        public PinnedMemory(T[] value, bool zero = true, bool locked = true, SystemType type = SystemType.Unknown)
        {
            if (!Types.ContainsKey(typeof(T)))
                throw new ArrayTypeMismatchException(
                    $"{nameof(T)} is not a supported pinned memory type. Supported types are: {string.Join(", ", Types.Keys.Select(t => t.Name))}");

            _buffer = value;
            Length = value.Length;
            _locked = locked;
            Types.TryGetValue(typeof(T), out _size);

            IntPtr pointer;
            UIntPtr context;
            switch (type)
            {
                case SystemType.Windows:
                    _memory = new WindowsMemory();
                    break;
                case SystemType.Linux:
                    _memory = new LinuxMemory();
                    break;
                case SystemType.Osx:
                    _memory = new OsxMemory();
                    break;
                default: // Find via try / fail
                    lock (_lock)
                    {
                        var buffer = new byte[1];
                        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                        try
                        {
                            pointer = handle.AddrOfPinnedObject();
                            context = new UIntPtr(1);

                            try
                            {
                                _memory = new WindowsMemory();
                                _memory.Zero(pointer, context);
                            }
                            catch (DllNotFoundException)
                            {
                                try
                                {
                                    _memory = new LinuxMemory();
                                    _memory.Zero(pointer, context);
                                }
                                catch (DllNotFoundException)
                                {
                                    try
                                    {
                                        _memory = new OsxMemory();
                                        _memory.Zero(pointer, context);
                                    }
                                    catch (DllNotFoundException)
                                    {
                                        throw new NotSupportedException(
                                            "No pinned memory support for current operating system");
                                    }
                                }
                            }
                        }
                        finally
                        {
                            handle.Free();
                        }

                        break;
                    }
            }

            _handle = GCHandle.Alloc(value, GCHandleType.Pinned);
            pointer = _handle.AddrOfPinnedObject();
            context = new UIntPtr((uint)(_size * Length));

            if (zero)
                _memory.Zero(pointer, context);
            if (_locked)
                _memory.Lock(pointer, context);
        }

        public T[] Read()
        {
            return _buffer;
        }

        public T Read(int index)
        {
            return _buffer[index];
        }

        public void Write(int index, T value)
        {
            _buffer[index] = value;
        }

        public PinnedMemory<T> Clone()
        {
            var buffer = (T[])_buffer.Clone();
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
            if (_handle == default(GCHandle))
                return;

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
            }
        }
    }
}

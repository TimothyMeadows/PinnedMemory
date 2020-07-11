/*
 * Based on original idea found here:
 * https://stackoverflow.com/questions/1166952/net-secure-memory-structures/38552838#38552838
 * Linux, and OSX improvements implemented from here:
 * https://github.com/mheyman/Isopoh.Cryptography.Argon2/tree/master/Isopoh.Cryptography.SecureArray
 */

using System;
using System.Runtime.InteropServices;

namespace PinnedMemory
{
    public class WindowsMemory : MemoryBase
    {
        private static readonly object Is32BitSubsystemLock = new object();
        private static readonly object GetProcessWorkingSetSizeLock = new object();
        private static readonly object SetProcessWorkingSetSizeLock = new object();
        private static readonly object VirtualAllocLock = new object();
        private static bool? _is32BitSubsystem;
        private static GetProcessWorkingSetSizeExDelegate _getProcessWorkingSetSize;
        private static Func<IntPtr, ulong, ulong, uint, bool> _setProcessWorkingSetSize;
        private static Func<IntPtr, ulong, uint, uint, IntPtr> _virtualAlloc;

        [DllImport("psapi.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr hProcess, out ProcessMemoryCounters counters, uint size);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll",
            EntryPoint = "GetProcessWorkingSetSizeEx",
            CallingConvention = CallingConvention.Winapi,
            SetLastError = true)]
        private static extern bool GetProcessWorkingSetSizeEx64(
            IntPtr processHandle,
            ref ulong minWorkingSetSize,
            ref ulong maxWorkingSetSize,
            ref uint flags);

        [DllImport("kernel32.dll",
            EntryPoint = "GetProcessWorkingSetSizeEx",
            CallingConvention = CallingConvention.Winapi,
            SetLastError = true)]
        private static extern bool GetProcessWorkingSetSizeEx32(
            IntPtr processHandle,
            ref uint minWorkingSetSize,
            ref uint maxWorkingSetSize,
            ref uint flags);

        private static bool GetProcessWorkingSetSizeEx32Wrapper(
            IntPtr processHandle,
            ref ulong minWorkingSetSize,
            ref ulong maxWorkingSetSize,
            ref uint flags)
        {
            var min = minWorkingSetSize > uint.MaxValue ? uint.MaxValue : (uint)minWorkingSetSize;
            var max = maxWorkingSetSize > uint.MaxValue ? uint.MaxValue : (uint)maxWorkingSetSize;
            var ret = GetProcessWorkingSetSizeEx32(processHandle, ref min, ref max, ref flags);
            minWorkingSetSize = min;
            maxWorkingSetSize = max;
            return ret;
        }

        [DllImport("kernel32.dll",
            EntryPoint = "SetProcessWorkingSetSizeEx",
            CallingConvention = CallingConvention.Winapi,
            SetLastError = true)]
        private static extern bool SetProcessWorkingSetSizeEx64(
            IntPtr processHandle,
            ulong minWorkingSetSize,
            ulong maxWorkingSetSize,
            uint flags);

        [DllImport("kernel32.dll",
            EntryPoint = "SetProcessWorkingSetSizeEx",
            CallingConvention = CallingConvention.Winapi,
            SetLastError = true)]
        private static extern bool SetProcessWorkingSetSizeEx32(
            IntPtr processHandle,
            uint minWorkingSetSize,
            uint maxWorkingSetSize,
            uint flags);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern void RtlZeroMemory(IntPtr ptr, UIntPtr cnt);

        [DllImport("kernel32.dll",
            EntryPoint = "VirtualAlloc",
            CallingConvention = CallingConvention.Winapi,
            SetLastError = true)]
        private static extern IntPtr VirtualAlloc64(
            IntPtr lpAddress,
            ulong size,
            uint allocationTypeFlags,
            uint protoectFlags);

        [DllImport("kernel32.dll",
            EntryPoint = "VirtualAlloc",
            CallingConvention = CallingConvention.Winapi,
            SetLastError = true)]
        private static extern IntPtr VirtualAlloc32(
            IntPtr lpAddress,
            uint size,
            uint allocationTypeFlags,
            uint protoectFlags);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        private static extern bool VirtualLock(IntPtr lpAddress, UIntPtr dwSize);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        private static extern bool VirtualUnlock(IntPtr lpAddress, UIntPtr dwSize);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        private static extern void SetLastError(uint dwErrorCode);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        [DllImport("kernel32.dll", SetLastError = true)]
        // ReSharper disable once UnusedMember.Local
        private static extern int VirtualQuery(IntPtr lpAddress, out MemoryBasicInformation lpBuffer, uint dwLength);

        [StructLayout(LayoutKind.Sequential, Size = 72)]
        public struct ProcessMemoryCounters
        {
            // ReSharper disable FieldCanBeMadeReadOnly.Local
            // ReSharper disable MemberCanBePrivate.Local

            public uint Cb;
            public uint PageFaultCount;
            public ulong PeakWorkingSetSize;
            public ulong WorkingSetSize;
            public ulong QuotaPeakPagedPoolUsage;
            public ulong QuotaPagedPoolUsage;
            public ulong QuotaPeakNonPagedPoolUsage;
            public ulong QuotaNonPagedPoolUsage;
            public ulong PagefileUsage;
            public ulong PeakPagefileUsage;

            // ReSharper restore MemberCanBePrivate.Local
            // ReSharper restore FieldCanBeMadeReadOnly.Local
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryBasicInformation
        {
            // ReSharper disable FieldCanBeMadeReadOnly.Local
            // ReSharper disable MemberCanBePrivate.Local

            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;

            // ReSharper restore MemberCanBePrivate.Local
            // ReSharper restore FieldCanBeMadeReadOnly.Local
        }

        private static bool Is32BitSubsystem
        {
            // TODO: Can Environment be trusted for this?
            get
            {
                if (_is32BitSubsystem.HasValue)
                    return _is32BitSubsystem.Value;
                lock (Is32BitSubsystemLock)
                {
                    if (_is32BitSubsystem.HasValue)
                        return _is32BitSubsystem.Value;
                    if (IntPtr.Size == 4)
                    {
                        _is32BitSubsystem = true;
                    }
                    else
                    {
                        var kernelModuleHandle = GetModuleHandle("kernel32");
                        if (kernelModuleHandle == IntPtr.Zero)
                        {
                            _is32BitSubsystem = true;
                        }
                        else
                        {
                            if (GetProcAddress(kernelModuleHandle, "IsWow64Process") == IntPtr.Zero)
                                _is32BitSubsystem = true;
                            else
                                _is32BitSubsystem = IsWow64Process(GetCurrentProcess(), out var isWow64Process) &&
                                                    isWow64Process;
                        }
                    }
                }

                return _is32BitSubsystem.Value;
            }
        }

        public WindowsMemory() : base(RtlZeroMemory, null, (m, l) => VirtualUnlock(m, l))
        {
            Lock = WindowsLockMemory;
        }

        private delegate bool GetProcessWorkingSetSizeExDelegate(IntPtr processHandle, ref ulong minWorkingSetSize,
            ref ulong maxWorkingSetSize, ref uint flags);

        private static GetProcessWorkingSetSizeExDelegate GetProcessWorkingSetSizeEx
        {
            get
            {
                if (_getProcessWorkingSetSize != null)
                    return _getProcessWorkingSetSize;

                lock (GetProcessWorkingSetSizeLock)
                {
                    if (_getProcessWorkingSetSize == null)
                        _getProcessWorkingSetSize =
                            Is32BitSubsystem
                                ? GetProcessWorkingSetSizeEx32Wrapper
                                : (GetProcessWorkingSetSizeExDelegate)GetProcessWorkingSetSizeEx64;
                }

                return _getProcessWorkingSetSize;
            }
        }

        private static Func<IntPtr, ulong, ulong, uint, bool> SetProcessWorkingSetSizeEx
        {
            get
            {
                if (_setProcessWorkingSetSize != null)
                    return _setProcessWorkingSetSize;

                lock (SetProcessWorkingSetSizeLock)
                {
                    if (_setProcessWorkingSetSize == null)
                        _setProcessWorkingSetSize = Is32BitSubsystem
                            ? (processHandle, minWorkingSetSize, maxWorkingSetSize, flags) =>
                            {
                                var min = minWorkingSetSize > uint.MaxValue ? uint.MaxValue : (uint)minWorkingSetSize;
                                var max = maxWorkingSetSize > uint.MaxValue ? uint.MaxValue : (uint)maxWorkingSetSize;
                                return SetProcessWorkingSetSizeEx32(
                                    processHandle,
                                    min,
                                    max,
                                    flags);
                            }
                        : (Func<IntPtr, ulong, ulong, uint, bool>)
                            SetProcessWorkingSetSizeEx64;
                }

                return _setProcessWorkingSetSize;
            }
        }

        private Func<IntPtr, ulong, uint, uint, IntPtr> VirtualAlloc
        {
            get
            {
                if (_virtualAlloc != null)
                    return _virtualAlloc;

                lock (VirtualAllocLock)
                {
                    if (_virtualAlloc == null)
                        _virtualAlloc = Is32BitSubsystem
                            ? (lpAddress, size, allocationTypeFlags, protectFlags) =>
                            {
                                if (size <= uint.MaxValue)
                                    return VirtualAlloc32(lpAddress, (uint)size, allocationTypeFlags, protectFlags);

                                SetLastError(8); // ERROR_NOT_ENOUGH_MEMORY
                                return IntPtr.Zero;
                            }
                        : (Func<IntPtr, ulong, uint, uint, IntPtr>)VirtualAlloc64;
                }

                return _virtualAlloc;
            }
        }

        private static ulong GetWorkingSetSize(IntPtr processHandle)
        {
            var memoryCounters = new ProcessMemoryCounters { Cb = (uint)Marshal.SizeOf<ProcessMemoryCounters>() };

            return GetProcessMemoryInfo(processHandle, out memoryCounters, memoryCounters.Cb)
                ? memoryCounters.WorkingSetSize
                : 0;
        }

        private string WindowsLockMemory(IntPtr m, UIntPtr l)
        {
            var processHandle = GetCurrentProcess();
            ulong prevMinVal = 0;
            ulong prevMaxVal = 0;
            uint prevFlags = 0;
            if (!GetProcessWorkingSetSizeEx(processHandle, ref prevMinVal, ref prevMaxVal, ref prevFlags))
                return $"Failed to get process working set size: Error: code={Marshal.GetLastWin32Error()}.";

            var prevCur = GetWorkingSetSize(processHandle);
            var newMaxWorkingSetSize = (ulong)((prevCur + l.ToUInt64()) * 1.2);
            if (!SetProcessWorkingSetSizeEx(processHandle, prevMinVal, newMaxWorkingSetSize, prevFlags))
            {
                var code = Marshal.GetLastWin32Error();
                return
                    $"Failed to set process working set size to {newMaxWorkingSetSize} (min={prevMinVal}, max={prevMaxVal}, flags={prevFlags}, cur={prevCur}) bytes at 0x{m.ToInt64():X8}. Error: code={code}.";
            }

            var cur = GetWorkingSetSize(processHandle);
            ulong minVal = 0;
            ulong maxVal = 0;
            uint flags = 0;
            if (!GetProcessWorkingSetSizeEx(processHandle, ref minVal, ref maxVal, ref flags))
            {
                var code = Marshal.GetLastWin32Error();
                return $"Failed to get process working set size: Error: code={code}.";
            }

            if (!VirtualLock(m, l))
            {
                var code = Marshal.GetLastWin32Error();
                var err = code == 1453 ? "Insufficient quota to complete the requested service" : $"code={code}";
                return $"Failed to securely lock {l.ToUInt64()} (prevMin={prevMinVal}, min={minVal}, "
                       + $"prevMax={prevMaxVal}, max={maxVal}, prevFlags={prevFlags}, flags={flags}, "
                       + $"prevCur={prevCur}, cur={cur}) bytes at 0x{m.ToInt64():X8}. Error: {err}.";
            }

            return null;
        }
    }
}

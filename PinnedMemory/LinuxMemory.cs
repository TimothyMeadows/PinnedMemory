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
    public class LinuxMemory : MemoryBase
    {
        [DllImport("libc", SetLastError = true, EntryPoint = "mlock")]
        private static extern int LinuxMlock(IntPtr addr, UIntPtr len);

        [DllImport("libc", SetLastError = true, EntryPoint = "munlock")]
        private static extern int LinuxMunlock(IntPtr addr, UIntPtr len);

        [DllImport("libc", EntryPoint = "memset")]
        private static extern IntPtr LinuxMemset(IntPtr addr, int c, UIntPtr n);

        [DllImport("libc", EntryPoint = "getrlimit", SetLastError = true)]
        private static extern int LinuxGetRLimit(int resource, ref LinuxRlimit rlimit);

        [DllImport("libc", EntryPoint = "setrlimit", SetLastError = true)]
        private static extern int LinuxSetRLimit(int resource, ref LinuxRlimit rlimit);

        [DllImport("libc", EntryPoint = "strerror_r", CharSet = CharSet.Ansi)]
        private static extern IntPtr LinuxSterrorR(int errno, IntPtr buf, ulong buflen);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct LinuxRlimit
        {
            // ReSharper disable FieldCanBeMadeReadOnly.Local
            // ReSharper disable MemberCanBePrivate.Local

            public ulong RlimCur;
            public ulong RlimMax;

            // ReSharper restore MemberCanBePrivate.Local
            // ReSharper restore FieldCanBeMadeReadOnly.Local
        }

        public LinuxMemory() : base((m, l) => LinuxMemset(m, 0, l), LinuxLockMemory, (m, l) => LinuxMunlock(m, l))
        {
        }

        private static string LinuxLockMemory(IntPtr m, UIntPtr l)
        {
            if (LinuxMlock(m, l) != 0)
            {
                var code = Marshal.GetLastWin32Error();
                if (LinuxTryRaiseCurrentMlockLimit(out var raiseError))
                {
                    if (LinuxMlock(m, l) == 0)
                        return null;

                    code = Marshal.GetLastWin32Error();
                }

                return $"memory lock error: {LinuxStrError(code)}{(raiseError == null ? string.Empty : $" ({raiseError})")}";
            }

            return null;
        }

        private static bool LinuxTryRaiseCurrentMlockLimit(out string error)
        {
            var rlimit = new LinuxRlimit { RlimCur = 0, RlimMax = 0 };
            var rlimitMemlock = 8;
            var success = false;
            if (LinuxGetRLimit(rlimitMemlock, ref rlimit) != 0)
            {
                error = $"attempted getrlimit(RLIMIT_MEMLOCK), got error: {LinuxStrError(Marshal.GetLastWin32Error())}.";
                return false;
            }

            if (rlimit.RlimCur < rlimit.RlimMax)
            {
                rlimit.RlimCur = rlimit.RlimMax;
                if (LinuxSetRLimit(rlimitMemlock, ref rlimit) != 0)
                {
                    error = $"attempted setrlimit(RLIMIT_MEMLOCK, {{{rlimit.RlimCur}, {rlimit.RlimMax}}}), got error: {LinuxStrError(Marshal.GetLastWin32Error())}.";
                    return false;
                }

                success = true;
            }

            var currentMax = rlimit.RlimMax;
            rlimit.RlimCur = ulong.MaxValue;
            rlimit.RlimMax = ulong.MaxValue;
            if (LinuxSetRLimit(rlimitMemlock, ref rlimit) == 0)
            {
                error = null;
                return true;
            }

            error = $"attempted setrlimit(RLIMIT_MEMLOCK, {{{rlimit.RlimCur}, {rlimit.RlimMax}}}) on current max {currentMax} bytes, got error: {LinuxStrError(Marshal.GetLastWin32Error())}.";
            return success;
        }

        private static string LinuxStrError(int errno)
        {
            var buf = new byte[256];
            var bufHandle = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try
            {
                var bufPtr = LinuxSterrorR(errno, bufHandle.AddrOfPinnedObject(), (ulong)buf.Length);
                return Marshal.PtrToStringAnsi(bufPtr);
            }
            finally
            {
                bufHandle.Free();
            }
        }
    }
}

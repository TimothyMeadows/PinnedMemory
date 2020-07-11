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
    public class OsxMemory : MemoryBase
    {
        public OsxMemory() : base((m, l) => OsxMemset(m, 0, l),
            (m, l) => OsxMlock(m, l) != 0 ? $"memory lock error code: {Marshal.GetLastWin32Error()}" : null,
            (m, l) => OsxMunlock(m, l))
        {
        }

        [DllImport("libSystem", SetLastError = true, EntryPoint = "mlock")]
        private static extern int OsxMlock(IntPtr addr, UIntPtr len);

        [DllImport("libSystem", SetLastError = true, EntryPoint = "munlock")]
        private static extern int OsxMunlock(IntPtr addr, UIntPtr len);

        [DllImport("libSystem", EntryPoint = "memset")]
        private static extern IntPtr OsxMemset(IntPtr addr, int c, UIntPtr n);
    }
}

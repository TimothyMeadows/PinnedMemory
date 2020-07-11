/*
 * Based on original idea found here:
 * https://stackoverflow.com/questions/1166952/net-secure-memory-structures/38552838#38552838
 * Linux, and OSX improvements implemented from here:
 * https://github.com/mheyman/Isopoh.Cryptography.Argon2/tree/master/Isopoh.Cryptography.SecureArray
 */

using System;

namespace PinnedMemory
{
    public class MemoryBase
    {
        public MemoryBase(Action<IntPtr, UIntPtr> zeroed, Func<IntPtr, UIntPtr, string> locked, Action<IntPtr, UIntPtr> unlocked)
        {
            Zero = zeroed;
            Lock = locked;
            Unlock = unlocked;
        }

        public Action<IntPtr, UIntPtr> Zero { get; protected set; }
        public Func<IntPtr, UIntPtr, string> Lock { get; protected set; }
        public Action<IntPtr, UIntPtr> Unlock { get; protected set; }
    }
}

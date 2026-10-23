using System;
using System.Runtime.InteropServices;

namespace Brante.Gamepad
{
    // Unity 2018.3 shows its startup logos from native code: each step compares the time since the logo began
    // with the logo's duration (at least two seconds) and moves on only when it has passed. Replacing that
    // "not yet" branch with NOPs makes every step advance, so the sequence ends within a few frames. The
    // branch is found by byte pattern in UnityPlayer.dll's code; if the pattern is not found exactly once
    // (a different Unity build), nothing is changed.
    internal static class SplashPatch
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string name);
        [DllImport("kernel32.dll")] private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protect, out uint previous);
        [DllImport("kernel32.dll")] private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();

        // comisd xmm1,[duration]; jb <keep waiting>; cmp [state],1; jz <next logo>; mov ecx,3
        // -1 is a wildcard (the rip-relative operands differ between builds).
        private static readonly int[] Pattern =
        {
            0x66, 0x0F, 0x2F, 0x0D, -1, -1, -1, -1,
            0x72, 0x6D,
            0x83, 0x3D, -1, -1, -1, -1, 0x01,
            0x74, 0x23,
            0xB9, 0x03, 0x00, 0x00, 0x00
        };
        private const int BranchOffset = 8;

        // Returns null when the patch is applied (or already was), otherwise the reason it was not.
        internal static string Apply()
        {
            IntPtr module = GetModuleHandleW("UnityPlayer.dll");
            if (module == IntPtr.Zero) return "UnityPlayer.dll is not loaded";
            int peOffset = Marshal.ReadInt32(module, 0x3C);
            if (Marshal.ReadInt32(module, peOffset) != 0x00004550) return "unexpected module header";
            int sections = Marshal.ReadInt16(module, peOffset + 6);
            int optionalSize = Marshal.ReadInt16(module, peOffset + 20);
            IntPtr table = IntPtr.Add(module, peOffset + 24 + optionalSize);
            for (int i = 0; i < sections; i++)
            {
                IntPtr header = IntPtr.Add(table, i * 40);
                if (Marshal.ReadInt64(header) != 0x747865742E) continue; // ".text"
                int size = Marshal.ReadInt32(header, 8), start = Marshal.ReadInt32(header, 12);
                return Patch(IntPtr.Add(module, start), size);
            }
            return "no .text section";
        }

        private static string Patch(IntPtr code, int size)
        {
            byte[] image = new byte[size];
            Marshal.Copy(code, image, 0, size);
            int found = -1, patched = -1;
            for (int at = 0; at + Pattern.Length <= size; at++)
            {
                if (image[at] != Pattern[0] && image[at] != 0x90) continue;
                if (Matches(image, at, false)) { if (found >= 0) return "pattern is not unique"; found = at; }
                else if (Matches(image, at, true)) patched = at;
            }
            if (found < 0) return patched >= 0 ? null : "pattern not found";
            IntPtr branch = IntPtr.Add(code, found + BranchOffset);
            uint previous;
            if (!VirtualProtect(branch, (UIntPtr)2, 0x40, out previous)) return "VirtualProtect failed";
            Marshal.WriteByte(branch, 0, 0x90);
            Marshal.WriteByte(branch, 1, 0x90);
            uint ignored;
            VirtualProtect(branch, (UIntPtr)2, previous, out ignored);
            FlushInstructionCache(GetCurrentProcess(), branch, (UIntPtr)2);
            return null;
        }

        // alreadyPatched: the two branch bytes are the NOPs written by an earlier Apply.
        private static bool Matches(byte[] image, int at, bool alreadyPatched)
        {
            for (int i = 0; i < Pattern.Length; i++)
            {
                if (Pattern[i] < 0) continue;
                int expected = alreadyPatched && (i == BranchOffset || i == BranchOffset + 1) ? 0x90 : Pattern[i];
                if (image[at + i] != expected) return false;
            }
            return true;
        }
    }
}

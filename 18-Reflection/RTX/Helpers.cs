using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;

namespace RayTracingTutorial18.RTX
{
    public static class Helpers
    {
        public static unsafe void MemCpy<T>(IntPtr destination, T[] source, uint count)
            where T : struct
        {
            GCHandle gcHandle = GCHandle.Alloc(source, GCHandleType.Pinned);
            Unsafe.CopyBlock((void*)destination, (void*)gcHandle.AddrOfPinnedObject(), count);
            gcHandle.Free();
        }

        public static unsafe void MemCpy<T>(IntPtr destination, T source, uint count)
        {
            IntPtr sourcePtr = (IntPtr)Unsafe.AsPointer(ref source);
            Unsafe.CopyBlock((void*)destination, (void*)sourcePtr, count);
        }

        public static Matrix3x4 ToMatrix3x4(this Matrix4x4 m)
        {
            return new Matrix3x4()
            {
                M11 = m.M11,
                M12 = m.M12,
                M13 = m.M13,
                M14 = m.M14,
                M21 = m.M21,
                M22 = m.M22,
                M23 = m.M23,
                M24 = m.M24,
                M31 = m.M31,
                M32 = m.M32,
                M33 = m.M33,
                M34 = m.M34,
            };
        }
    }
}

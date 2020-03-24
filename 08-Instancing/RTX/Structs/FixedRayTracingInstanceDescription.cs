using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;

namespace RayTracingTutorial08.Structs
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FixedRaytracingInstanceDescription
    {
        [FieldOffset(0)]
        public Matrix3x4 Transform;
        [FieldOffset(48)]
        public int InstanceID;
        [FieldOffset(51)]
        public byte InstanceMask;
        [FieldOffset(52)]
        public int InstanceContributionToHitGroupIndex;
        [FieldOffset(55)]
        public byte Flags;
        [FieldOffset(56)]
        public long AccelerationStructure;
    }
}

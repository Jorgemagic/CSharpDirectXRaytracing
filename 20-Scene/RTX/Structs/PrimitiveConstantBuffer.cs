using System.Numerics;
using System.Runtime.InteropServices;

namespace RayTracingTutorial20.Structs
{
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct PrimitiveConstantBuffer
    {
        [FieldOffset(0)]
        public Vector4 diffuseColor;

        [FieldOffset(16)]
        public float inShadowRadiance;
        
        [FieldOffset(20)]
        public float diffuseCoef;

        [FieldOffset(24)]
        public float specularCoef;

        [FieldOffset(28)]
        public float specularPower;

        [FieldOffset(32)]
        public float reflectanceCoef;
        
    }
}


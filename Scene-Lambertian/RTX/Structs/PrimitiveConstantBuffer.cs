using System.Numerics;
using System.Runtime.InteropServices;

namespace SceneLambertian.Structs
{
    public enum MaterialTypes
    {
        Lambertian = 0,
        Metal = 1,
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct PrimitiveConstantBuffer
    {
        [FieldOffset(0)]
        public Vector4 diffuseColor;

        [FieldOffset(16)]
        public MaterialTypes materialType;
        
        [FieldOffset(20)]
        public float fuzz;      
    }
}


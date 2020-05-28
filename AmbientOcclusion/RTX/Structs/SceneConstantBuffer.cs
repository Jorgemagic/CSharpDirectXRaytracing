using System.Numerics;
using System.Runtime.InteropServices;

namespace AmbientOcclusion.Structs
{
    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public struct SceneConstantBuffer
    {
        [FieldOffset(0)]
        public Matrix4x4 projectionToWorld;

        [FieldOffset(64)]
        public Vector4 backgroundColor;

        [FieldOffset(80)]
        public Vector3 cameraPosition;

        [FieldOffset(92)]
        public float MaxRecursionDepth;

        [FieldOffset(96)]
        public Vector3 lightPosition;

        [FieldOffset(108)]
        public int FrameCount;

        [FieldOffset(112)]
        public Vector4 lightAmbientColor;

        [FieldOffset(128)]
        public Vector4 lightDiffuseColor;

    }
}

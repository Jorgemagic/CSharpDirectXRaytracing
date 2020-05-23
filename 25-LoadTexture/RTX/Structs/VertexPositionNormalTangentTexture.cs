using System.Numerics;

namespace RayTracingTutorial25.RTX.Structs
{
    public struct VertexPositionNormalTangentTexture
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector3 Tangent;
        public Vector2 TexCoord;

        public VertexPositionNormalTangentTexture(Vector3 position, Vector3 normal, Vector3 tangent, Vector2 texCoord)
        {
            this.Position = position;
            this.Normal = normal;
            this.Tangent = tangent;
            this.TexCoord = texCoord;
        }        
    }
}

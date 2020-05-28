using AmbientOcclusion.RTX.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AmbientOcclusion.RTX
{
    public static class Primitives
    {
        public static void Sphere(float diameter, int tessellation, out List<VertexPositionNormalTangentTexture> vertexData, out List<ushort> indexData, bool uvHorizontalFlip = false, bool uvVerticalFlip = false)
        {
            vertexData = new List<VertexPositionNormalTangentTexture>();
            indexData = new List<ushort>();

            if (tessellation < 3)
            {
                throw new ArgumentOutOfRangeException("tessellation");
            }

            int verticalSegments = tessellation;
            int horizontalSegments = tessellation * 2;
            float uIncrement = 1f / horizontalSegments;
            float vIncrement = 1f / verticalSegments;
            float radius = diameter / 2;

            uIncrement *= uvHorizontalFlip ? 1 : -1;
            vIncrement *= uvVerticalFlip ? 1 : -1;

            float u = uvHorizontalFlip ? 0 : 1;
            float v = uvVerticalFlip ? 0 : 1;

            // Start with a single vertex at the bottom of the sphere.
            for (int i = 0; i < horizontalSegments; i++)
            {
                u += uIncrement;

                // this.AddVertex(new Vector3(0,-1,0) * radius, new Vector3(0,-1,0), new Vector2(u, v));
                vertexData.Add(new VertexPositionNormalTangentTexture(new Vector3(0,-1,0) * radius, new Vector3(0,-1,0), Vector3.Zero, new Vector2(u, v)));
            }

            // Create rings of vertices at progressively higher latitudes.
            v = uvVerticalFlip ? 0 : 1;
            for (int i = 0; i < verticalSegments - 1; i++)
            {
                float latitude = (((i + 1) * (float)Math.PI) / verticalSegments) - (float)Math.PI / 2;
                u = uvHorizontalFlip ? 0 : 1;
                v += vIncrement;
                float dy = (float)Math.Sin(latitude);
                float dxz = (float)Math.Cos(latitude);

                // Create a single ring of vertices at this latitude.
                for (int j = 0; j <= horizontalSegments; j++)
                {
                    float longitude = j * (float)Math.PI * 2 / horizontalSegments;

                    float dx = (float)Math.Cos(longitude) * dxz;
                    float dz = (float)Math.Sin(longitude) * dxz;

                    Vector3 normal = new Vector3(dx, dy, dz);

                    Vector2 texCoord = new Vector2(u, v);
                    u += uIncrement;

                    // this.AddVertex(normal * radius, normal, texCoord);
                    vertexData.Add(new VertexPositionNormalTangentTexture(normal * radius, normal, Vector3.Zero, texCoord));
                }
            }

            // Finish with a single vertex at the top of the sphere.
            v = uvVerticalFlip ? 1 : 0;
            u = uvHorizontalFlip ? 0 : 1;
            for (int i = 0; i < horizontalSegments; i++)
            {
                u += uIncrement;

                // this.AddVertex(new Vector3(0,1,0) * radius, new Vector3(0,1,0), new Vector2(u, v));
                vertexData.Add(new VertexPositionNormalTangentTexture(new Vector3(0,1,0) * radius, new Vector3(0,1,0), Vector3.Zero, new Vector2(u, v)));
            }

            // Create a fan connecting the bottom vertex to the bottom latitude ring.
            for (int i = 0; i < horizontalSegments; i++)
            {
                // this.AddIndex(i);
                indexData.Add((ushort)i);

                // this.AddIndex(1 + i + horizontalSegments);
                indexData.Add((ushort)(1 + i + horizontalSegments));

                // this.AddIndex(i + horizontalSegments);
                indexData.Add((ushort)(i + horizontalSegments));
            }

            // Fill the sphere body with triangles joining each pair of latitude rings.
            for (int i = 0; i < verticalSegments - 2; i++)
            {
                for (int j = 0; j < horizontalSegments; j++)
                {
                    int nextI = i + 1;
                    int nextJ = j + 1;
                    int num = horizontalSegments + 1;

                    int i1 = horizontalSegments + (i * num) + j;
                    int i2 = horizontalSegments + (i * num) + nextJ;
                    int i3 = horizontalSegments + (nextI * num) + j;
                    int i4 = i3 + 1;

                    // this.AddIndex(i1);
                    indexData.Add((ushort)i1);

                    // this.AddIndex(i2);
                    indexData.Add((ushort)i2);

                    // this.AddIndex(i3);
                    indexData.Add((ushort)i3);

                    // this.AddIndex(i2);
                    indexData.Add((ushort)i2);

                    // this.AddIndex(i4);
                    indexData.Add((ushort)i4);

                    // this.AddIndex(i3);
                    indexData.Add((ushort)i3);
                }
            }

            // Create a fan connecting the top vertex to the top latitude ring.
            for (int i = 0; i < horizontalSegments; i++)
            {
                // this.AddIndex(this.VerticesCount - 1 - i);
                indexData.Add((ushort)(vertexData.Count - 1 - i));

                // this.AddIndex(this.VerticesCount - horizontalSegments - 2 - i);
                indexData.Add((ushort)(vertexData.Count - horizontalSegments - 2 - i));

                // this.AddIndex(this.VerticesCount - horizontalSegments - 1 - i);
                indexData.Add((ushort)(vertexData.Count - horizontalSegments - 1 - i));
            }

            CalculateTangentSpace(vertexData, indexData);
        }

        public static void Torus(float diameter, float thickness, int tessellation, out List<VertexPositionNormalTangentTexture> vertexData, out List<ushort> indexData)
        {
            vertexData = new List<VertexPositionNormalTangentTexture>();
            indexData = new List<ushort>();

            if (tessellation < 3)
            {
                throw new ArgumentOutOfRangeException("tessellation");
            }

            int tessellationPlus = tessellation + 1;

            // First we loop around the main ring of the torus.
            for (int i = 0; i <= tessellation; i++)
            {
                float outerPercent = i / (float)tessellation;
                float outerAngle = outerPercent * (float)Math.PI * 2;

                // Create a transform matrix that will align geometry to
                // slice perpendicularly though the current ring position.
                Matrix4x4 transform = Matrix4x4.CreateTranslation(diameter / 2, 0, 0) *
                                   Matrix4x4.CreateRotationY(outerAngle);

                // Now we loop along the other axis, around the side of the tube.
                for (int j = 0; j <= tessellation; j++)
                {
                    float innerPercent = j / (float)tessellation;
                    float innerAngle = (float)Math.PI * 2 * innerPercent;

                    float dx = (float)Math.Cos(innerAngle);
                    float dy = (float)Math.Sin(innerAngle);

                    // Create a vertex.
                    Vector3 normal = new Vector3(dx, dy, 0);
                    Vector3 position = normal * thickness / 2;

                    position = Vector3.Transform(position, transform);
                    normal = Vector3.TransformNormal(normal, transform);

                    // this.AddVertex(position, normal, new Vector2(outerPercent, 0.5f - innerPercent));
                    vertexData.Add(new VertexPositionNormalTangentTexture(position, normal, Vector3.Zero, new Vector2(outerPercent, 0.5f - innerPercent)));

                    // And create indices for two triangles.
                    int nextI = (i + 1) % tessellationPlus;
                    int nextJ = (j + 1) % tessellationPlus;

                    if ((j < tessellation) && (i < tessellation))
                    {
                        // this.AddIndex((i * tessellationPlus) + j);
                        indexData.Add((ushort)((i * tessellationPlus) + j));

                        // this.AddIndex((i * tessellationPlus) + nextJ);
                        indexData.Add((ushort)((i * tessellationPlus) + nextJ));

                        // this.AddIndex((nextI * tessellationPlus) + j);
                        indexData.Add((ushort)((nextI * tessellationPlus) + j));

                        // this.AddIndex((i * tessellationPlus) + nextJ);
                        indexData.Add((ushort)((i * tessellationPlus) + nextJ));

                        // this.AddIndex((nextI * tessellationPlus) + nextJ);
                        indexData.Add((ushort)((nextI * tessellationPlus) + nextJ));

                        // this.AddIndex((nextI * tessellationPlus) + j);
                        indexData.Add((ushort)((nextI * tessellationPlus) + j));
                    }
                }
            }

            CalculateTangentSpace(vertexData, indexData);
        }

        public static void Capsule(float height, float radius, int tessellation, out List<VertexPositionNormalTangentTexture> vertexData, out List<ushort> indexData)
        {
            vertexData = new List<VertexPositionNormalTangentTexture>();
            indexData = new List<ushort>();

            if (tessellation % 2 != 0)
            {
                throw new ArgumentOutOfRangeException("tessellation should be even");
            }

            int halfTessellation = tessellation / 2;

            int stride = 0;

            // Start with a single vertex at the bottom of the sphere.
            // this.AddVertex((new Vector3(0,-1,0) * radius) + (new Vector3(0,-1,0) * 0.5f * height), new Vector3(0,-1,0), new Vector2(0.5f, 0.5f));
            vertexData.Add(new VertexPositionNormalTangentTexture((new Vector3(0,-1,0) * radius) + (new Vector3(0,-1,0) * 0.5f * height), new Vector3(0,-1,0), Vector3.Zero, new Vector2(0.5f, 0.5f)));

            // Create the lower sphere
            for (int i = 1; i <= halfTessellation; i++)
            {
                float vPercentaje = 1 - (i / (float)halfTessellation);
                float latitude = vPercentaje * (float)Math.PI / 2;
                float dy = -(float)Math.Sin(latitude);
                float dxz = (float)Math.Cos(latitude);

                for (int j = 0; j < tessellation; j++)
                {
                    float hPercentaje = j / (float)tessellation;
                    float longitude = hPercentaje * (float)Math.PI * 2;

                    float dx = (float)Math.Cos(longitude) * dxz;
                    float dz = (float)Math.Sin(longitude) * dxz;

                    var normal = new Vector3(dx, dy, dz);
                    Vector3 position = normal * radius;

                    position += new Vector3(0,-1,0) * 0.5f * height;

                    // this.AddVertex(position, normal, new Vector2(1 - ((0.5f * dx) + 0.5f), (0.5f * dz) + 0.5f));
                    vertexData.Add(new VertexPositionNormalTangentTexture(position, normal, Vector3.Zero, new Vector2(1 - ((0.5f * dx) + 0.5f), (0.5f * dz) + 0.5f)));

                    if (i < halfTessellation)
                    {
                        int nextI = i + 1;
                        int nextJ = (j + 1) % tessellation;

                        var i1 = 1 + ((i - 1) * tessellation) + j;
                        var i2 = 1 + ((i - 1) * tessellation) + nextJ;
                        var i3 = 1 + ((nextI - 1) * tessellation) + j;

                        var i4 = 1 + ((i - 1) * tessellation) + nextJ;
                        var i5 = 1 + ((nextI - 1) * tessellation) + nextJ;
                        var i6 = 1 + ((nextI - 1) * tessellation) + j;

                        // this.AddIndex(i1);
                        indexData.Add((ushort)i1);

                        // this.AddIndex(i2);
                        indexData.Add((ushort)i2);

                        // this.AddIndex(i3);
                        indexData.Add((ushort)i3);

                        // this.AddIndex(i4);
                        indexData.Add((ushort)i4);

                        // this.AddIndex(i5);
                        indexData.Add((ushort)i5);

                        // this.AddIndex(i6);
                        indexData.Add((ushort)i6);
                    }
                }
            }

            stride = vertexData.Count;
            float cylinderHeight = height * 0.5f;

            // Creates the cylinder
            for (int i = 0; i <= tessellation; i++)
            {
                float percent = i / (float)tessellation;
                float angle = percent * (float)Math.PI * 2;

                float dx = (float)Math.Cos(angle);
                float dz = (float)Math.Sin(angle);

                Vector3 normal = new Vector3(dx, 0, dz);

                // this.AddVertex((normal * radius) + (new Vector3(0,1,0) * cylinderHeight), normal, new Vector2(1 - percent, 0));
                vertexData.Add(new VertexPositionNormalTangentTexture((normal * radius) + (new Vector3(0,1,0) * cylinderHeight), normal, Vector3.Zero, new Vector2(1 - percent, 0)));

                // this.AddVertex((normal * radius) + (new Vector3(0,-1,0) * cylinderHeight), normal, new Vector2(1 - percent, 1));
                vertexData.Add(new VertexPositionNormalTangentTexture((normal * radius) + (new Vector3(0,-1,0) * cylinderHeight), normal, Vector3.Zero, new Vector2(1 - percent, 1)));

                if (i < tessellation)
                {
                    // this.AddIndex(stride + (i * 2));
                    indexData.Add((ushort)(stride + (i * 2)));

                    // this.AddIndex(stride + ((i * 2) + 1));
                    indexData.Add((ushort)(stride + ((i * 2) + 1)));

                    // this.AddIndex(stride + (((i * 2) + 2) % ((tessellation + 1) * 2)));
                    indexData.Add((ushort)(stride + (((i * 2) + 2) % ((tessellation + 1) * 2))));

                    // this.AddIndex(stride + ((i * 2) + 1));
                    indexData.Add((ushort)(stride + ((i * 2) + 1)));

                    // this.AddIndex(stride + (((i * 2) + 3) % ((tessellation + 1) * 2)));
                    indexData.Add((ushort)(stride + (((i * 2) + 3) % ((tessellation + 1) * 2))));

                    // this.AddIndex(stride + (((i * 2) + 2) % ((tessellation + 1) * 2)));
                    indexData.Add((ushort)(stride + (((i * 2) + 2) % ((tessellation + 1) * 2))));
                }
            }

            stride = vertexData.Count;

            // Creates a single vertex at the top of the sphere.
            // this.AddVertex((new Vector3(0,1,0) * radius) + (new Vector3(0,1,0) * 0.5f * height), new Vector3(0,1,0), new Vector2(0.5f, 0.5f));
            vertexData.Add(new VertexPositionNormalTangentTexture((new Vector3(0,1,0) * radius) + (new Vector3(0,1,0) * 0.5f * height), new Vector3(0,1,0), Vector3.Zero, new Vector2(0.5f, 0.5f)));

            // Create the upper sphere
            for (int i = 1; i <= halfTessellation; i++)
            {
                float vPercentaje = 1 - (i / (float)halfTessellation);
                float latitude = vPercentaje * (float)Math.PI / 2;
                float dy = (float)Math.Sin(latitude);
                float dxz = (float)Math.Cos(latitude);

                for (int j = 0; j < tessellation; j++)
                {
                    float hPercentaje = j / (float)tessellation;
                    float longitude = hPercentaje * (float)Math.PI * 2;

                    float dx = (float)Math.Cos(longitude) * dxz;
                    float dz = (float)Math.Sin(longitude) * dxz;

                    var normal = new Vector3(dx, dy, dz);
                    Vector3 position = normal * radius;

                    position += new Vector3(0,1,0) * 0.5f * height;

                    // this.AddVertex(position, normal, new Vector2((0.5f * dx) + 0.5f, (0.5f * dz) + 0.5f));
                    vertexData.Add(new VertexPositionNormalTangentTexture(position, normal, Vector3.Zero, new Vector2((0.5f * dx) + 0.5f, (0.5f * dz) + 0.5f)));

                    if (i < halfTessellation)
                    {
                        int nextI = i + 1;
                        int nextJ = (j + 1) % tessellation;

                        var i1 = 1 + ((i - 1) * tessellation) + j;
                        var i2 = 1 + ((nextI - 1) * tessellation) + j;
                        var i3 = 1 + ((i - 1) * tessellation) + nextJ;

                        var i4 = 1 + ((i - 1) * tessellation) + nextJ;
                        var i5 = 1 + ((nextI - 1) * tessellation) + j;
                        var i6 = 1 + ((nextI - 1) * tessellation) + nextJ;

                        // this.AddIndex(i1 + stride);
                        indexData.Add((ushort)(i1 + stride));

                        // this.AddIndex(i2 + stride);
                        indexData.Add((ushort)(i2 + stride));

                        // this.AddIndex(i3 + stride);
                        indexData.Add((ushort)(i3 + stride));

                        // this.AddIndex(i4 + stride);
                        indexData.Add((ushort)(i4 + stride));

                        // this.AddIndex(i5 + stride);
                        indexData.Add((ushort)(i5 + stride));

                        // this.AddIndex(i6 + stride);
                        indexData.Add((ushort)(i6 + stride));
                    }
                }
            }

            // Create a fan connecting the bottom vertex to the bottom latitude ring.
            for (int i = 0; i < tessellation; i++)
            {
                // this.AddIndex(0);
                indexData.Add((ushort)0);

                // this.AddIndex(1 + ((i + 1) % tessellation));
                indexData.Add((ushort)(1 + ((i + 1) % tessellation)));

                // this.AddIndex(i + 1);
                indexData.Add((ushort)(i + 1));
            }

            // Create a fan connecting the top vertex to the top latitude ring.
            for (int i = 0; i < tessellation; i++)
            {
                // this.AddIndex(stride);
                indexData.Add((ushort)stride);

                // this.AddIndex(stride + i + 1);
                indexData.Add((ushort)(stride + i + 1));

                // this.AddIndex(stride + 1 + ((i + 1) % tessellation));
                indexData.Add((ushort)(stride + 1 + ((i + 1) % tessellation)));
            }

            CalculateTangentSpace(vertexData, indexData);
        }

        public static void Cube(float size, out List<VertexPositionNormalTangentTexture> vertexData, out List<ushort> indexData, bool uvHorizontalFlip = false, bool uvVerticalFlip = false, float uTileFactor = 1, float vTileFactor = 1)
        {
            vertexData = new List<VertexPositionNormalTangentTexture>();
            indexData = new List<ushort>();

            float uCoordMin = uvHorizontalFlip ? uTileFactor : 0;
            float uCoordMax = uvHorizontalFlip ? 0 : uTileFactor;
            float vCoordMin = uvVerticalFlip ? vTileFactor : 0;
            float vCoordMax = uvVerticalFlip ? 0 : vTileFactor;

            // A cube has six faces, each one pointing in a different direction.
            Vector3[] normals =
            {
                new Vector3(0, 0, 1),
                new Vector3(0, 0, -1),
                new Vector3(1, 0, 0),
                new Vector3(-1, 0, 0),
                new Vector3(0, 1, 0),
                new Vector3(0, -1, 0),
            };

            Vector2[] texCoord =
            {
                new Vector2(uCoordMax, vCoordMax), new Vector2(uCoordMin, vCoordMax), new Vector2(uCoordMin, vCoordMin), new Vector2(uCoordMax, vCoordMin),
                new Vector2(uCoordMin, vCoordMin), new Vector2(uCoordMax, vCoordMin), new Vector2(uCoordMax, vCoordMax), new Vector2(uCoordMin, vCoordMax),
                new Vector2(uCoordMax, vCoordMin), new Vector2(uCoordMax, vCoordMax), new Vector2(uCoordMin, vCoordMax), new Vector2(uCoordMin, vCoordMin),
                new Vector2(uCoordMax, vCoordMin), new Vector2(uCoordMax, vCoordMax), new Vector2(uCoordMin, vCoordMax), new Vector2(uCoordMin, vCoordMin),
                new Vector2(uCoordMin, vCoordMax), new Vector2(uCoordMin, vCoordMin), new Vector2(uCoordMax, vCoordMin), new Vector2(uCoordMax, vCoordMax),
                new Vector2(uCoordMax, vCoordMin), new Vector2(uCoordMax, vCoordMax), new Vector2(uCoordMin, vCoordMax), new Vector2(uCoordMin, vCoordMin),
            };

            Vector3[] tangents =
            {
                new Vector3(1, 0, 0),
                new Vector3(-1, 0, 0),
                new Vector3(0, 0, -1),
                new Vector3(0, 0, 1),
                new Vector3(1, 0, 0),
                new Vector3(1, 0, 0),
            };

            // Create each face in turn.
            for (int i = 0, j = 0; i < normals.Length; i++, j += 4)
            {
                Vector3 normal = normals[i];
                Vector3 tangent = tangents[i];

                // Get two vectors perpendicular to the face normal and to each other.
                Vector3 side1 = new Vector3(normal.Y, normal.Z, normal.X);
                Vector3 side2 = Vector3.Cross(normal, side1);

                // Six indices (two triangles) per face.
                // this.AddIndex(this.VerticesCount + 0);
                indexData.Add((ushort)(vertexData.Count + 0));

                // this.AddIndex(this.VerticesCount + 1);
                indexData.Add((ushort)(vertexData.Count + 1));

                // this.AddIndex(this.VerticesCount + 3);
                indexData.Add((ushort)(vertexData.Count + 3));

                // this.AddIndex(this.VerticesCount + 1);
                indexData.Add((ushort)(vertexData.Count + 1));

                // this.AddIndex(this.VerticesCount + 2);
                indexData.Add((ushort)(vertexData.Count + 2));

                // this.AddIndex(this.VerticesCount + 3);
                indexData.Add((ushort)(vertexData.Count + 3));

                // 0   3
                // 1   2
                float sideOverTwo = size * 0.5f;

                // Four vertices per face.
                // this.AddVertex((normal - side1 - side2) * sideOverTwo, normal, tangent, texCoord[j]);
                vertexData.Add(new VertexPositionNormalTangentTexture((normal - side1 - side2) * sideOverTwo, normal, tangent, texCoord[j]));

                // this.AddVertex((normal - side1 + side2) * sideOverTwo, normal, tangent, texCoord[j + 1]);
                vertexData.Add(new VertexPositionNormalTangentTexture((normal - side1 + side2) * sideOverTwo, normal, tangent, texCoord[j + 1]));

                // this.AddVertex((normal + side1 + side2) * sideOverTwo, normal, tangent, texCoord[j + 2]);
                vertexData.Add(new VertexPositionNormalTangentTexture((normal + side1 + side2) * sideOverTwo, normal, tangent, texCoord[j + 2]));

                // this.AddVertex((normal + side1 - side2) * sideOverTwo, normal, tangent, texCoord[j + 3]);
                vertexData.Add(new VertexPositionNormalTangentTexture((normal + side1 - side2) * sideOverTwo, normal, tangent, texCoord[j + 3]));
            }

            CalculateTangentSpace(vertexData, indexData);
        }

        public static void Quad(int size, out List<VertexPositionNormalTangentTexture> vertexData, out List<ushort> indexData, bool uvHorizontalFlip = false, bool uvVerticalFlip = false, float uTileFactor = 1, float vTileFactor = 1)
        {
            VertexPositionNormalTangentTexture[] vertices = new VertexPositionNormalTangentTexture[]
            {
                // Indexed Quad
                new VertexPositionNormalTangentTexture(new Vector3(-size, 0, -size), Vector3.UnitY, Vector3.Zero, Vector2.Zero),
                new VertexPositionNormalTangentTexture(new Vector3( size, 0, -size), Vector3.UnitY, Vector3.Zero, Vector2.Zero),
                new VertexPositionNormalTangentTexture(new Vector3( size, 0,  size), Vector3.UnitY, Vector3.Zero, Vector2.Zero),
                new VertexPositionNormalTangentTexture(new Vector3(-size, 0,  size), Vector3.UnitY, Vector3.Zero, Vector2.Zero),
            };

            vertexData = vertices.ToList();
            var indices = new ushort[] { 0, 1, 2, 0, 2, 3 };
            indexData = indices.ToList();
        }

        public static void Pyramid(float size, out List<VertexPositionNormalTangentTexture> vertexData, out List<ushort> indexData)
        {
            vertexData = new List<VertexPositionNormalTangentTexture>();
            indexData = new List<ushort>();

            Vector3 basePos = new Vector3(0,-1,0);
            float sizeOverTwo = size / 2;

            // Get two vectors perpendicular to the face normal.
            Vector3 side1 = new Vector3(basePos.Y, basePos.Z, basePos.X);
            Vector3 side2 = Vector3.Cross(basePos, side1);

            // Six indices (two triangles) for down face.
            // this.AddIndex(0);
            indexData.Add((ushort)0);

            // this.AddIndex(1);
            indexData.Add((ushort)1);

            // this.AddIndex(2);
            indexData.Add((ushort)2);

            // this.AddIndex(0);
            indexData.Add((ushort)0);

            // this.AddIndex(2);
            indexData.Add((ushort)2);

            // this.AddIndex(3);
            indexData.Add((ushort)3);

            // Twelve indices for rest of face
            // this.AddIndex(4);
            indexData.Add((ushort)4);

            // this.AddIndex(5);
            indexData.Add((ushort)5);

            // this.AddIndex(6);
            indexData.Add((ushort)6);

            // this.AddIndex(7);
            indexData.Add((ushort)7);

            // this.AddIndex(8);
            indexData.Add((ushort)8);

            // this.AddIndex(9);
            indexData.Add((ushort)9);

            // this.AddIndex(10);
            indexData.Add((ushort)10);

            // this.AddIndex(11);
            indexData.Add((ushort)11);

            // this.AddIndex(12);
            indexData.Add((ushort)12);

            // this.AddIndex(13);
            indexData.Add((ushort)13);

            // this.AddIndex(14);
            indexData.Add((ushort)14);

            // this.AddIndex(15);
            indexData.Add((ushort)15);

            // 0   3
            // 1   2
            // Four vertices for a face.
            Vector3 normal = new Vector3(0,-1,0);

            // this.AddVertex((basePos - side1 - side2) * sizeOverTwo, normal, new Vector2(1, 0));
            vertexData.Add(new VertexPositionNormalTangentTexture((basePos - side1 - side2) * sizeOverTwo, normal, Vector3.Zero, new Vector2(1, 0)));

            // this.AddVertex((basePos - side1 + side2) * sizeOverTwo, normal, new Vector2(1, 1));
            vertexData.Add(new VertexPositionNormalTangentTexture((basePos - side1 + side2) * sizeOverTwo, normal, Vector3.Zero, new Vector2(1, 1)));

            // this.AddVertex((basePos + side1 + side2) * sizeOverTwo, normal, new Vector2(0, 1));
            vertexData.Add(new VertexPositionNormalTangentTexture((basePos + side1 + side2) * sizeOverTwo, normal, Vector3.Zero, new Vector2(0, 1)));

            // this.AddVertex((basePos + side1 - side2) * sizeOverTwo, normal, new Vector2(0, 0));
            vertexData.Add(new VertexPositionNormalTangentTexture((basePos + side1 - side2) * sizeOverTwo, normal, Vector3.Zero, new Vector2(0, 0)));

            // First triangle
            normal = new Vector3(0, 0.5f, 1);
            normal = Vector3.Normalize(normal);

            // this.AddVertex((basePos - side1 - side2) * sizeOverTwo, normal, new Vector2(1, 1));
            vertexData.Add(new VertexPositionNormalTangentTexture((basePos - side1 - side2) * sizeOverTwo, normal, Vector3.Zero, new Vector2(1, 1)));

            // this.AddVertex((basePos + side1 - side2) * sizeOverTwo, normal, new Vector2(0, 1));
            vertexData.Add(new VertexPositionNormalTangentTexture((basePos + side1 - side2) * sizeOverTwo, normal, Vector3.Zero, new Vector2(0, 1)));

            // this.AddVertex(basePos * -(size / 2), normal, new Vector2(0.5f, 0));
            vertexData.Add(new VertexPositionNormalTangentTexture(basePos * -(size / 2), normal, Vector3.Zero, new Vector2(0.5f, 0)));

            // Second triangle
            normal = new Vector3(-1, 0.5f, 0);
            normal = Vector3.Normalize(normal);

            // this.AddVertex((basePos + side1 - side2) * sizeOverTwo, normal, new Vector2(1, 1));
            vertexData.Add(new VertexPositionNormalTangentTexture((basePos + side1 - side2) * sizeOverTwo, normal, Vector3.Zero, new Vector2(1, 1)));

            // this.AddVertex((basePos + side1 + side2) * sizeOverTwo, normal, new Vector2(0, 1));
            vertexData.Add(new VertexPositionNormalTangentTexture((basePos + side1 + side2) * sizeOverTwo, normal, Vector3.Zero, new Vector2(0, 1)));

            // this.AddVertex(basePos * -(size / 2), normal, new Vector2(0.5f, 0));
            vertexData.Add(new VertexPositionNormalTangentTexture(basePos * -(size / 2), normal, Vector3.Zero, new Vector2(0.5f, 0)));

            // Thrid triangle
            normal = new Vector3(0, 0.5f, -1);
            normal = Vector3.Normalize(normal);

            // this.AddVertex((basePos + side1 + side2) * sizeOverTwo, normal, new Vector2(1, 1));
            vertexData.Add(new VertexPositionNormalTangentTexture((basePos + side1 + side2) * sizeOverTwo, normal, Vector3.Zero, new Vector2(1, 1)));

            // this.AddVertex((basePos - side1 + side2) * sizeOverTwo, normal, new Vector2(0, 1));
            vertexData.Add(new VertexPositionNormalTangentTexture((basePos - side1 + side2) * sizeOverTwo, normal, Vector3.Zero, new Vector2(0, 1)));

            // this.AddVertex(basePos * -(size / 2), normal, new Vector2(0.5f, 0));
            vertexData.Add(new VertexPositionNormalTangentTexture(basePos * -(size / 2), normal, Vector3.Zero, new Vector2(0.5f, 0)));

            // Fourth triangle
            normal = new Vector3(1, 0.5f, 0);
            normal = Vector3.Normalize(normal);

            // this.AddVertex((basePos - side1 + side2) * sizeOverTwo, normal, new Vector2(1, 1));
            vertexData.Add(new VertexPositionNormalTangentTexture((basePos - side1 + side2) * sizeOverTwo, normal, Vector3.Zero, new Vector2(1, 1)));

            // this.AddVertex((basePos - side1 - side2) * sizeOverTwo, normal, new Vector2(0, 1));
            vertexData.Add(new VertexPositionNormalTangentTexture((basePos - side1 - side2) * sizeOverTwo, normal, Vector3.Zero, new Vector2(0, 1)));

            // this.AddVertex(basePos * -(size / 2), normal, new Vector2(0.5f, 0));
            vertexData.Add(new VertexPositionNormalTangentTexture(basePos * -(size / 2), normal, Vector3.Zero, new Vector2(0.5f, 0)));

            CalculateTangentSpace(vertexData, indexData);
        }

        public static void Plane(float size, out List<VertexPositionNormalTangentTexture> vertexData, out List<ushort> indexData)
        {
            vertexData = new List<VertexPositionNormalTangentTexture>();
            indexData = new List<ushort>();

            Vector3 position = Vector3.Zero;
            Vector3 normal = Vector3.UnitZ;
            Vector3 up = Vector3.UnitY;

            Matrix4x4 matrix = Matrix4x4.CreateLookAt(position, normal, up);

            // Get two vectors perpendicular to the face normal.
            Vector3 side1 = 0.5f * size * Vector3.UnitX;
            Vector3 side2 = 0.5f * size * Vector3.UnitY;

            Vector3 v1 = -side1 - side2;
            Vector3 v2 = -side1 + side2;
            Vector3 v3 = side1 + side2;
            Vector3 v4 = side1 - side2;

            v1 = Vector3.Transform(v1, matrix);
            v2 = Vector3.Transform(v2, matrix);
            v3 = Vector3.Transform(v3, matrix);
            v4 = Vector3.Transform(v4, matrix);

            Vector2[] uv = new Vector2[4];
            uv[0] = new Vector2(0, 1);
            uv[1] = new Vector2(0, 0);
            uv[2] = new Vector2(1, 0);
            uv[3] = new Vector2(1, 1);

            // Front Faces
            indexData.Add(0);
            indexData.Add(1);
            indexData.Add(2);

            indexData.Add(0);
            indexData.Add(2);
            indexData.Add(3);

            vertexData.Add(new VertexPositionNormalTangentTexture(v1, normal, Vector3.Zero, uv[0]));
            vertexData.Add(new VertexPositionNormalTangentTexture(v2, normal, Vector3.Zero, uv[1]));
            vertexData.Add(new VertexPositionNormalTangentTexture(v3, normal, Vector3.Zero, uv[2]));
            vertexData.Add(new VertexPositionNormalTangentTexture(v4, normal, Vector3.Zero, uv[3]));

            // Back Face
            indexData.Add(4);
            indexData.Add(6);
            indexData.Add(5);

            indexData.Add(4);
            indexData.Add(7);
            indexData.Add(6);

            vertexData.Add(new VertexPositionNormalTangentTexture(v1, normal, Vector3.Zero, uv[3]));
            vertexData.Add(new VertexPositionNormalTangentTexture(v2, normal, Vector3.Zero, uv[2]));
            vertexData.Add(new VertexPositionNormalTangentTexture(v3, normal, Vector3.Zero, uv[1]));
            vertexData.Add(new VertexPositionNormalTangentTexture(v4, normal, Vector3.Zero, uv[0]));

            CalculateTangentSpace(vertexData, indexData);
        }

        private unsafe static void CalculateTangentSpace(List<VertexPositionNormalTangentTexture> vertices, List<ushort> indices)
        {
            int vertexCount = vertices.Count;
            int triangleCount = indices.Count / 3;

            Vector3* tan1 = stackalloc Vector3[vertexCount * 2];
            Vector3* tan2 = tan1 + vertexCount;

            VertexPositionNormalTangentTexture a1, a2, a3;
            Vector3 v1, v2, v3;
            Vector2 w1, w2, w3;

            for (int a = 0; a < triangleCount; a++)
            {
                ushort i1 = indices[(a * 3) + 0];
                ushort i2 = indices[(a * 3) + 1];
                ushort i3 = indices[(a * 3) + 2];

                a1 = vertices[i1];
                a2 = vertices[i2];
                a3 = vertices[i3];

                v1 = a1.Position;
                v2 = a2.Position;
                v3 = a3.Position;

                w1 = a1.TexCoord;
                w2 = a2.TexCoord;
                w3 = a3.TexCoord;

                float x1 = v2.X - v1.X;
                float x2 = v3.X - v1.X;
                float y1 = v2.Y - v1.Y;
                float y2 = v3.Y - v1.Y;
                float z1 = v2.Z - v1.Z;
                float z2 = v3.Z - v1.Z;

                float s1 = w2.X - w1.X;
                float s2 = w3.X - w1.X;
                float t1 = w2.Y - w1.Y;
                float t2 = w3.Y - w1.Y;

                float r = 1.0F / ((s1 * t2) - (s2 * t1));
                Vector3 sdir = new Vector3(((t2 * x1) - (t1 * x2)) * r, ((t2 * y1) - (t1 * y2)) * r, ((t2 * z1) - (t1 * z2)) * r);
                Vector3 tdir = new Vector3(((s1 * x2) - (s2 * x1)) * r, ((s1 * y2) - (s2 * y1)) * r, ((s1 * z2) - (s2 * z1)) * r);

                tan1[i1] += sdir;
                tan1[i2] += sdir;
                tan1[i3] += sdir;

                tan2[i1] += tdir;
                tan2[i2] += tdir;
                tan2[i3] += tdir;
            }

            for (int a = 0; a < vertexCount; a++)
            {
                var vertex = vertices[a];

                Vector3 n = vertex.Normal;
                Vector3 t = tan1[a];

                // Gram-Schmidt orthogonalize
                vertex.Tangent = t - (n * Vector3.Dot(n, t));
                vertex.Tangent = Vector3.Normalize(vertex.Tangent);
                vertices[a] = vertex;
            }
        }

        public static void InvertFaces(ref List<ushort> indexData)
        {
            for (int i = 0; i < indexData.Count; i += 3)
            {
                ushort aux = indexData[i + 1];
                indexData[i + 1] = indexData[i + 2];
                indexData[i + 2] = aux;
            }
        }
    }
}

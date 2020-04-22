using RayTracingTutorial17.RTX.Structs;
using RayTracingTutorial17.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace RayTracingTutorial17.RTX
{
    public class AccelerationStructures
    {
        public enum PrimitiveType
        {
            Cube,
            Sphere,
            Plane,
            Pyramid,
            Torus,
            Quad,
        }

        public static HeapProperties kUploadHeapProps = new HeapProperties(HeapType.Upload, CpuPageProperty.Unknown, MemoryPool.Unknown, 0, 0);
        public static HeapProperties kDefaultHeapProps = new HeapProperties(HeapType.Default, CpuPageProperty.Unknown, MemoryPool.Unknown, 0, 0);
        public ID3D12Resource VertexBuffer;
        public uint VertexCount;
        public ID3D12Resource IndexBuffer;
        public uint IndexCount;

        public ID3D12Resource CreateBuffer(ID3D12Device5 pDevice, uint size, ResourceFlags flags, ResourceStates initState, HeapProperties heapProps)
        {
            ResourceDescription bufDesc = new ResourceDescription();
            bufDesc.Alignment = 0;
            bufDesc.DepthOrArraySize = 1;
            bufDesc.Dimension = ResourceDimension.Buffer;
            bufDesc.Flags = flags;
            bufDesc.Format = Format.Unknown;
            bufDesc.Height = 1;
            bufDesc.Layout = TextureLayout.RowMajor;
            bufDesc.MipLevels = 1;
            bufDesc.SampleDescription = new SampleDescription(1, 0);
            bufDesc.Width = size;

            ID3D12Resource pBuffer = pDevice.CreateCommittedResource(heapProps, HeapFlags.None, bufDesc, initState, null);
            return pBuffer;
        }

        public void CreatePrimitive(PrimitiveType primitiveType, ID3D12Device5 pDevice, out ID3D12Resource vertexBuffer, out uint vertexCount, out ID3D12Resource indexBuffer, out uint indexCount)
        {
            List<VertexPositionNormalTangentTexture> vertexList = null;
            List<ushort> indexList = null;

            switch (primitiveType)
            {
                case PrimitiveType.Cube:
                    Primitives.Cube(1.5f, out vertexList, out indexList);
                    break;
                case PrimitiveType.Sphere:
                    Primitives.Sphere(1.0f, 64, out vertexList, out indexList);
                    break;
                case PrimitiveType.Plane:
                    Primitives.Plane(3.0f, out vertexList, out indexList);
                    break;
                case PrimitiveType.Quad:
                    Primitives.Quad(100, out vertexList, out indexList);
                    break;
                case PrimitiveType.Pyramid:
                    Primitives.Pyramid(1.5f, out vertexList, out indexList);
                    break;
                case PrimitiveType.Torus:
                    Primitives.Torus(2.0f, 0.6f, 32, out vertexList, out indexList);
                    break;
                default:
                    break;
            }

            var vertexData = vertexList.ToArray();
            var indexData = indexList.ToArray();

            // Vertex buffer
            vertexCount = (uint)vertexData.Length;
            vertexBuffer = CreateBuffer(pDevice, (uint)(Unsafe.SizeOf<VertexPositionNormalTangentTexture>() * vertexData.Length), ResourceFlags.None, ResourceStates.GenericRead, kUploadHeapProps);
            IntPtr pData = vertexBuffer.Map(0, null);
            Helpers.MemCpy(pData, vertexData, (uint)(Unsafe.SizeOf<VertexPositionNormalTangentTexture>() * vertexData.Length));
            vertexBuffer.Unmap(0, null);

            // Index buffer
            indexCount = (uint)indexData.Length;
            indexBuffer = CreateBuffer(pDevice, (uint)(sizeof(ushort) * indexData.Length), ResourceFlags.None, ResourceStates.GenericRead, kUploadHeapProps);
            IntPtr pIB = indexBuffer.Map(0, null);
            Helpers.MemCpy(pIB, indexData, (uint)(sizeof(ushort) * indexData.Length));
            indexBuffer.Unmap(0, null);
        }

        public AccelerationStructureBuffers CreatePlaneBottomLevelAS(ID3D12Device5 pDevice, ID3D12GraphicsCommandList4 pCmdList)
        {
            this.CreatePrimitive(PrimitiveType.Quad, pDevice, out ID3D12Resource planeVertexBuffer, out uint planeVertexCount, out ID3D12Resource planeIndexBuffer, out uint planeIndexCount);
            return this.CreateBottomLevelAS(pDevice, pCmdList, planeVertexBuffer, planeVertexCount, planeIndexBuffer, planeIndexCount);
        }

        public AccelerationStructureBuffers CreatePrimitiveBottomLevelAS(ID3D12Device5 pDevice, ID3D12GraphicsCommandList4 pCmdList)
        {
            this.CreatePrimitive(PrimitiveType.Sphere, pDevice, out ID3D12Resource primitiveVertexBuffer, out uint primitiveVertexCount, out ID3D12Resource primitiveIndexBuffer, out uint primitiveIndexCount);
            this.VertexBuffer = primitiveVertexBuffer;
            this.VertexCount = primitiveVertexCount;
            this.IndexBuffer = primitiveIndexBuffer;
            this.IndexCount = primitiveIndexCount;

            return this.CreateBottomLevelAS(pDevice, pCmdList, primitiveVertexBuffer, primitiveVertexCount, primitiveIndexBuffer, primitiveIndexCount);
        }

        private AccelerationStructureBuffers CreateBottomLevelAS(ID3D12Device5 pDevice, ID3D12GraphicsCommandList4 pCmdList, ID3D12Resource vb, uint vbCount, ID3D12Resource ib, uint ibCount)
        {
            int geometryCount = 1;
            RaytracingGeometryDescription[] geomDesc = new RaytracingGeometryDescription[geometryCount];

            // Primitives
            geomDesc[0].Type = RaytracingGeometryType.Triangles;
            geomDesc[0].Triangles = new RaytracingGeometryTrianglesDescription();
            geomDesc[0].Triangles.VertexBuffer = new GpuVirtualAddressAndStride();
            geomDesc[0].Triangles.VertexBuffer.StartAddress = vb.GPUVirtualAddress;
            geomDesc[0].Triangles.VertexBuffer.StrideInBytes = Unsafe.SizeOf<VertexPositionNormalTangentTexture>();
            geomDesc[0].Triangles.VertexFormat = Format.R32G32B32_Float;
            geomDesc[0].Triangles.VertexCount = (int)vbCount;
            geomDesc[0].Triangles.IndexBuffer = ib.GPUVirtualAddress;
            geomDesc[0].Triangles.IndexCount = (int)ibCount;
            geomDesc[0].Triangles.IndexFormat = Format.R16_UInt;
            geomDesc[0].Flags = RaytracingGeometryFlags.Opaque;


            // Get the size requirements for the scratch and AS buffers
            BuildRaytracingAccelerationStructureInputs inputs = new BuildRaytracingAccelerationStructureInputs();
            inputs.Layout = ElementsLayout.Array;
            inputs.Flags = RaytracingAccelerationStructureBuildFlags.None;
            inputs.DescriptorsCount = geometryCount;
            inputs.GeometryDescriptions = geomDesc;
            inputs.Type = RaytracingAccelerationStructureType.BottomLevel;

            RaytracingAccelerationStructurePrebuildInfo info;
            info = pDevice.GetRaytracingAccelerationStructurePrebuildInfo(inputs);

            // Create the buffers. They need to support UAV, and since we are going to immediately use them, we create them with an unordered-access state
            AccelerationStructureBuffers buffers = new AccelerationStructureBuffers();
            buffers.pScratch = this.CreateBuffer(pDevice, (uint)info.ScratchDataSizeInBytes, ResourceFlags.AllowUnorderedAccess, ResourceStates.UnorderedAccess, kDefaultHeapProps);
            buffers.pResult = this.CreateBuffer(pDevice, (uint)info.ResultDataMaxSizeInBytes, ResourceFlags.AllowUnorderedAccess, ResourceStates.RaytracingAccelerationStructure, kDefaultHeapProps);

            // Create the bottom-level AS
            BuildRaytracingAccelerationStructureDescription asDesc = new BuildRaytracingAccelerationStructureDescription();
            asDesc.Inputs = inputs;
            asDesc.DestinationAccelerationStructureData = buffers.pResult.GPUVirtualAddress;
            asDesc.ScratchAccelerationStructureData = buffers.pScratch.GPUVirtualAddress;

            pCmdList.BuildRaytracingAccelerationStructure(asDesc);

            // We need to insert a UAV barrier before using the acceleration structures in a raytracing operation
            ResourceBarrier uavBarrier = new ResourceBarrier(new ResourceUnorderedAccessViewBarrier(buffers.pResult));
            pCmdList.ResourceBarrier(uavBarrier);

            return buffers;
        }

        public unsafe AccelerationStructureBuffers CreateTopLevelAS(ID3D12Device5 pDevice, ID3D12GraphicsCommandList4 pCmdList, AccelerationStructureBuffers[] pBottomLevelAS, ref long tlasSize)
        {
            int instances = 2;

            // First, get the size of the TLAS buffers and create them
            BuildRaytracingAccelerationStructureInputs inputs = new BuildRaytracingAccelerationStructureInputs();
            inputs.Layout = ElementsLayout.Array;
            inputs.Flags = RaytracingAccelerationStructureBuildFlags.None;
            inputs.DescriptorsCount = instances;
            inputs.Type = RaytracingAccelerationStructureType.TopLevel;

            RaytracingAccelerationStructurePrebuildInfo info;
            info = pDevice.GetRaytracingAccelerationStructurePrebuildInfo(inputs);

            // Create the buffers
            AccelerationStructureBuffers buffers = new AccelerationStructureBuffers();
            buffers.pScratch = this.CreateBuffer(pDevice, (uint)info.ScratchDataSizeInBytes, ResourceFlags.AllowUnorderedAccess, ResourceStates.UnorderedAccess, kDefaultHeapProps);
            buffers.pResult = this.CreateBuffer(pDevice, (uint)info.ResultDataMaxSizeInBytes, ResourceFlags.AllowUnorderedAccess, ResourceStates.RaytracingAccelerationStructure, kDefaultHeapProps);
            tlasSize = info.ResultDataMaxSizeInBytes;

            // The instance desc should be inside a buffer, create and map the buffer
            buffers.pInstanceDesc = this.CreateBuffer(pDevice, (uint)(Unsafe.SizeOf<FixedRaytracingInstanceDescription>() * instances), ResourceFlags.None, ResourceStates.GenericRead, kUploadHeapProps);
            FixedRaytracingInstanceDescription[] pInstanceDesc = new FixedRaytracingInstanceDescription[instances];

            // The transformation matrices for the instances
            Matrix4x4[] transformation = new Matrix4x4[instances];
            transformation[0] = Matrix4x4.CreateTranslation(new Vector3(0, -1.0f, 0));
            transformation[1] = Matrix4x4.CreateScale(2.0f);

            pInstanceDesc[0].InstanceID = 0;                          // This value will be exposed to the shader via InstanceID()
            pInstanceDesc[0].InstanceContributionToHitGroupIndex = 0; // This is the offset inside the shader-table. We only have a single geometry, so the offset 0
            pInstanceDesc[0].Flags = (byte)RaytracingInstanceFlags.None;
            pInstanceDesc[0].Transform = Matrix4x4.Transpose(transformation[0]).ToMatrix3x4(); // GLM is column major, the INSTANCE_DESC  is row major
            pInstanceDesc[0].AccelerationStructure = pBottomLevelAS[0].pResult.GPUVirtualAddress;
            pInstanceDesc[0].InstanceMask = 0xff;

            for (int i = 1; i < instances; i++)
            {
                pInstanceDesc[i].InstanceID = i;                          // This value will be exposed to the shader via InstanceID()
                pInstanceDesc[i].InstanceContributionToHitGroupIndex = 0; // This is the offset inside the shader-table. We only have a single geometry, so the offset 0
                pInstanceDesc[i].Flags = (byte)RaytracingInstanceFlags.None;
                pInstanceDesc[i].Transform = Matrix4x4.Transpose(transformation[i]).ToMatrix3x4(); // GLM is column major, the INSTANCE_DESC  is row major
                pInstanceDesc[i].AccelerationStructure = pBottomLevelAS[1].pResult.GPUVirtualAddress;
                pInstanceDesc[i].InstanceMask = 0xff;
            }

            IntPtr data;
            data = buffers.pInstanceDesc.Map(0, null);
            Helpers.MemCpy(data, pInstanceDesc, (uint)(Unsafe.SizeOf<FixedRaytracingInstanceDescription>() * instances));
            buffers.pInstanceDesc.Unmap(0, null);

            // Create the TLAS
            BuildRaytracingAccelerationStructureDescription asDesc = new BuildRaytracingAccelerationStructureDescription();
            asDesc.Inputs = inputs;
            asDesc.Inputs.InstanceDescriptions = buffers.pInstanceDesc.GPUVirtualAddress;
            asDesc.DestinationAccelerationStructureData = buffers.pResult.GPUVirtualAddress;
            asDesc.ScratchAccelerationStructureData = buffers.pScratch.GPUVirtualAddress;

            pCmdList.BuildRaytracingAccelerationStructure(asDesc);

            // We need to insert a UAV barrier before using the acceleration structures in a raytracing operation
            ResourceBarrier uavBarrier = new ResourceBarrier(new ResourceUnorderedAccessViewBarrier(buffers.pResult));
            pCmdList.ResourceBarrier(uavBarrier);

            return buffers;
        }
    }
}

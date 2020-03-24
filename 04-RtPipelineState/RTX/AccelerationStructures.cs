using RayTracingTutorial04.Structs;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace RayTracingTutorial04.RTX
{
    public class AccelerationStructures
    {
        public static HeapProperties kUploadHeapProps = new HeapProperties(HeapType.Upload, CpuPageProperty.Unknown, MemoryPool.Unknown, 0, 0);
        public static HeapProperties kDefaultHeapProps = new HeapProperties(HeapType.Default, CpuPageProperty.Unknown, MemoryPool.Unknown, 0, 0);
        

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

        public ID3D12Resource CreateTriangleVB(ID3D12Device5 pDevice)
        {
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(0,      1,       0),
                new Vector3(0.866f, -0.5f,   0),
                new Vector3(-0.866f,-0.5f,   0),
            };

            // For simplicity, we create the vertex buffer on the upload heap, but that's not required
            ID3D12Resource pBuffer = CreateBuffer(pDevice, (uint)(Unsafe.SizeOf<Vector3>() * vertices.Length), ResourceFlags.None, ResourceStates.GenericRead, kUploadHeapProps);
            IntPtr pData = pBuffer.Map(0, null);
            Helpers.MemCpy(pData, vertices, (uint)(Unsafe.SizeOf<Vector3>() * vertices.Length));
            pBuffer.Unmap(0, null);

            return pBuffer;
        }

        public AccelerationStructureBuffers CreateBottomLevelAS(ID3D12Device5 pDevice, ID3D12GraphicsCommandList4 pCmdList, ID3D12Resource pVB)
        {
            RaytracingGeometryDescription geomDesc = new RaytracingGeometryDescription();
            geomDesc.Type = RaytracingGeometryType.Triangles;
            geomDesc.Triangles = new RaytracingGeometryTrianglesDescription();
            geomDesc.Triangles.VertexBuffer = new GpuVirtualAddressAndStride();
            geomDesc.Triangles.VertexBuffer.StartAddress = pVB.GPUVirtualAddress;
            geomDesc.Triangles.VertexBuffer.StrideInBytes = Unsafe.SizeOf<Vector3>();
            geomDesc.Triangles.VertexFormat = Format.R32G32B32_Float;
            geomDesc.Triangles.VertexCount = 3;
            geomDesc.Flags = RaytracingGeometryFlags.Opaque;

            // Get the size requirements for the scratch and AS buffers
            BuildRaytracingAccelerationStructureInputs inputs = new BuildRaytracingAccelerationStructureInputs();
            inputs.Layout = ElementsLayout.Array;
            inputs.Flags = RaytracingAccelerationStructureBuildFlags.None;
            inputs.DescriptorsCount = 1;
            inputs.GeometryDescriptions = new RaytracingGeometryDescription[] { geomDesc };
            inputs.Type = RaytracingAccelerationStructureType.BottomLevel;

            RaytracingAccelerationStructurePrebuildInfo info = new RaytracingAccelerationStructurePrebuildInfo();
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

        public AccelerationStructureBuffers CreateTopLevelAS(ID3D12Device5 pDevice, ID3D12GraphicsCommandList4 pCmdList, ID3D12Resource pBottomLevelAS, ref long tlasSize)
        {
            // First, get the size of the TLAS buffers and create them
            BuildRaytracingAccelerationStructureInputs inputs = new BuildRaytracingAccelerationStructureInputs();
            inputs.Layout = ElementsLayout.Array;
            inputs.Flags = RaytracingAccelerationStructureBuildFlags.None;
            inputs.DescriptorsCount = 1;
            inputs.Type = RaytracingAccelerationStructureType.TopLevel;

            RaytracingAccelerationStructurePrebuildInfo info;
            info = pDevice.GetRaytracingAccelerationStructurePrebuildInfo(inputs);

            // Create the buffers
            AccelerationStructureBuffers buffers = new AccelerationStructureBuffers();
            buffers.pScratch = this.CreateBuffer(pDevice, (uint)info.ScratchDataSizeInBytes, ResourceFlags.AllowUnorderedAccess, ResourceStates.UnorderedAccess, kDefaultHeapProps);
            buffers.pResult = this.CreateBuffer(pDevice, (uint)info.ResultDataMaxSizeInBytes, ResourceFlags.AllowUnorderedAccess, ResourceStates.RaytracingAccelerationStructure, kDefaultHeapProps);
            tlasSize = info.ResultDataMaxSizeInBytes;

            // The instance desc should be inside a buffer, create and map the buffer
            buffers.pInstanceDesc = this.CreateBuffer(pDevice, (uint)Unsafe.SizeOf<FixedRaytracingInstanceDescription>(), ResourceFlags.None, ResourceStates.GenericRead, kUploadHeapProps);
            FixedRaytracingInstanceDescription pInstanceDesc;

            // Initialize the instance desc. We only have a single instance
            pInstanceDesc.InstanceID = 0;                          // This value will be exposed to the shader via InstanceID()
            pInstanceDesc.InstanceContributionToHitGroupIndex = 0; // This is the offset inside the shader-table. We only have a single geometry, so the offset 0
            pInstanceDesc.Flags = (byte)RaytracingInstanceFlags.None;
            pInstanceDesc.Transform = Matrix4x4.Identity.ToMatrix3x4();
            pInstanceDesc.AccelerationStructure = pBottomLevelAS.GPUVirtualAddress;
            pInstanceDesc.InstanceMask = 0xff;

            IntPtr data;
            data = buffers.pInstanceDesc.Map(0, null);
            Helpers.MemCpy(data, pInstanceDesc, (uint)Unsafe.SizeOf<FixedRaytracingInstanceDescription>());
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

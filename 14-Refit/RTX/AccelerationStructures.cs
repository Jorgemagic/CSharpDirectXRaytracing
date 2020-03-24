using RayTracingTutorial14.Structs;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace RayTracingTutorial14.RTX
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

        public ID3D12Resource CreatePlaneVB(ID3D12Device5 pDevice)
        {
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-100, -1, -2),
                new Vector3( 100, -1 ,100),
                new Vector3(-100, -1, 100),

                new Vector3(-100, -1, -2),
                new Vector3( 100, -1, -2),
                new Vector3( 100, -1, 100),
            };

            // For simplicity, we create the vertex buffer on the upload heap, but that's not required
            ID3D12Resource pBuffer = CreateBuffer(pDevice, (uint)(Unsafe.SizeOf<Vector3>() * vertices.Length), ResourceFlags.None, ResourceStates.GenericRead, kUploadHeapProps);
            IntPtr pData = pBuffer.Map(0, null);
            Helpers.MemCpy(pData, vertices, (uint)(Unsafe.SizeOf<Vector3>() * vertices.Length));
            pBuffer.Unmap(0, null);

            return pBuffer;
        }

        public AccelerationStructureBuffers CreateBottomLevelAS(ID3D12Device5 pDevice, ID3D12GraphicsCommandList4 pCmdList, ID3D12Resource[] pVB, int[] vertexCount, int geometryCount)
        {
            RaytracingGeometryDescription[] geomDesc;
            geomDesc = new RaytracingGeometryDescription[geometryCount];

            for (int i = 0; i < geometryCount; i++)
            {
                geomDesc[i].Type = RaytracingGeometryType.Triangles;
                geomDesc[i].Triangles = new RaytracingGeometryTrianglesDescription();
                geomDesc[i].Triangles.VertexBuffer = new GpuVirtualAddressAndStride();
                geomDesc[i].Triangles.VertexBuffer.StartAddress = pVB[i].GPUVirtualAddress;
                geomDesc[i].Triangles.VertexBuffer.StrideInBytes = Unsafe.SizeOf<Vector3>();
                geomDesc[i].Triangles.VertexCount = vertexCount[i];
                geomDesc[i].Triangles.VertexFormat = Format.R32G32B32_Float;
                geomDesc[i].Flags = RaytracingGeometryFlags.Opaque;
            }

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

        public unsafe AccelerationStructureBuffers BuildTopLevelAS(ID3D12Device5 pDevice, ID3D12GraphicsCommandList4 pCmdList, ID3D12Resource[] pBottomLevelAS, ref long tlasSize, float rotation, bool update, ref AccelerationStructureBuffers buffers)
        {
            // First, get the size of the TLAS buffers and create them
            BuildRaytracingAccelerationStructureInputs inputs = new BuildRaytracingAccelerationStructureInputs();
            inputs.Layout = ElementsLayout.Array;
            inputs.Flags = RaytracingAccelerationStructureBuildFlags.AllowUpdate;
            inputs.DescriptorsCount = 3; // NumDescs
            inputs.Type = RaytracingAccelerationStructureType.TopLevel;

            RaytracingAccelerationStructurePrebuildInfo info;
            info = pDevice.GetRaytracingAccelerationStructurePrebuildInfo(inputs);

            if (update)
            {
                // If this a request for an update, then the TLAS was already used in a DispatchRay() call. We need a UAV barrier to make sure the read operation ends before updating the buffer
                ResourceBarrier uavBarrier1 = new ResourceBarrier(new ResourceUnorderedAccessViewBarrier(buffers.pResult));
                pCmdList.ResourceBarrier(uavBarrier1);
            }
            else
            {
                buffers.pScratch = this.CreateBuffer(pDevice, (uint)info.ScratchDataSizeInBytes, ResourceFlags.AllowUnorderedAccess, ResourceStates.UnorderedAccess, kDefaultHeapProps);
                buffers.pResult = this.CreateBuffer(pDevice, (uint)info.ResultDataMaxSizeInBytes, ResourceFlags.AllowUnorderedAccess, ResourceStates.RaytracingAccelerationStructure, kDefaultHeapProps);
                buffers.pInstanceDesc = this.CreateBuffer(pDevice, (uint)Unsafe.SizeOf<FixedRaytracingInstanceDescription>() * 3, ResourceFlags.None, ResourceStates.GenericRead, kUploadHeapProps);
                tlasSize = info.ResultDataMaxSizeInBytes;
            }

            FixedRaytracingInstanceDescription[] instanceDescs = new FixedRaytracingInstanceDescription[3];

            // The transformation matrices for the instances
            Matrix4x4[] transformation = new Matrix4x4[3];
            Matrix4x4 rotationMat = Matrix4x4.CreateRotationY(rotation);
            transformation[0] = Matrix4x4.Identity;
            transformation[1] = rotationMat * Matrix4x4.CreateTranslation(new Vector3(-2, 0, 0));
            transformation[2] = rotationMat * Matrix4x4.CreateTranslation(new Vector3(2, 0, 0));

            // The InstanceContributionToHitGroupIndex is set based on the shader-table layout specified in createShaderTable()
            // Create the desc for the triangle/plane instance
            instanceDescs[0].InstanceID = 0;
            instanceDescs[0].InstanceContributionToHitGroupIndex = 0;
            instanceDescs[0].Flags = (byte)RaytracingInstanceFlags.None;
            instanceDescs[0].Transform = Matrix4x4.Transpose(transformation[0]).ToMatrix3x4(); // GLM is column major, the INSTANCE_DESC  is row major
            instanceDescs[0].AccelerationStructure = pBottomLevelAS[0].GPUVirtualAddress;
            instanceDescs[0].InstanceMask = 0xFF;

            for (int i = 1; i < 3; i++)
            {
                instanceDescs[i].InstanceID = i;                          // This value will be exposed to the shader via InstanceID()
                instanceDescs[i].InstanceContributionToHitGroupIndex = (i * 2) + 2; // The plane takes an additional entry in the shader-table, hence the +1
                instanceDescs[i].Flags = (byte)RaytracingInstanceFlags.None;
                instanceDescs[i].Transform = Matrix4x4.Transpose(transformation[i]).ToMatrix3x4(); // GLM is column major, the INSTANCE_DESC  is row major
                instanceDescs[i].AccelerationStructure = pBottomLevelAS[1].GPUVirtualAddress;
                instanceDescs[i].InstanceMask = 0xFF;
            }

            // Map the instance desc buffer
            IntPtr data;
            data = buffers.pInstanceDesc.Map(0, null);
            Helpers.MemCpy(data, instanceDescs, (uint)Unsafe.SizeOf<FixedRaytracingInstanceDescription>() * 3);
            buffers.pInstanceDesc.Unmap(0, null);

            // Create the TLAS
            BuildRaytracingAccelerationStructureDescription asDesc = new BuildRaytracingAccelerationStructureDescription();
            asDesc.Inputs = inputs;
            asDesc.Inputs.InstanceDescriptions = buffers.pInstanceDesc.GPUVirtualAddress;
            asDesc.DestinationAccelerationStructureData = buffers.pResult.GPUVirtualAddress;
            asDesc.ScratchAccelerationStructureData = buffers.pScratch.GPUVirtualAddress;

            // If this is an update operation, set the source buffer and the perform_update flag
            if (update)
            {
                asDesc.Inputs.Flags |= RaytracingAccelerationStructureBuildFlags.PerformUpdate;
                asDesc.SourceAccelerationStructureData = buffers.pResult.GPUVirtualAddress;
            }

            pCmdList.BuildRaytracingAccelerationStructure(asDesc);

            // We need to insert a UAV barrier before using the acceleration structures in a raytracing operation
            ResourceBarrier uavBarrier = new ResourceBarrier(new ResourceUnorderedAccessViewBarrier(buffers.pResult));
            pCmdList.ResourceBarrier(uavBarrier);

            return buffers;
        }
    }
}

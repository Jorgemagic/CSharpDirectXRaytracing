using RayTracingTutorial21.RTX;
using RayTracingTutorial21.RTX.Structs;
using RayTracingTutorial21.Structs;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace RayTracingTutorial21
{
    public class Scene
    {
        private const int D3D12DefaultShader4ComponentMapping = 5768;
        private const int kRtvHeapSize = 3;
        private Color4 clearColor = new Color4(0.4f, 0.6f, 0.2f, 1.0f);

        private readonly Window Window;
        private D3D12GraphicsContext context;
        private IntPtr mHwnd;
        private ID3D12Device5 mpDevice;
        private ID3D12CommandQueue mpCmdQueue;
        private IDXGISwapChain3 mpSwapChain;
        private ID3D12GraphicsCommandList4 mpCmdList;
        private HeapData mRtvHeap;
        private FrameObject[] mFrameObjects;
        private ID3D12Fence mpFence;
        private uint mFenceValue = 0;
        private EventWaitHandle mFenceEvent;
        private Rect mSwapChainRect;
        private ID3D12Resource mpTopLevelAS;
        private ID3D12StateObject mpPipelineState;
        private ID3D12RootSignature mpEmptyRootSig;
        private AccelerationStructures acs;
        private ID3D12Resource mpOutputResource;
        private ID3D12DescriptorHeap mpSrvUavHeap;
        private ID3D12Resource mpShaderTable;
        private uint mShaderTableEntrySize;

        private ID3D12Resource sceneCB;
        private ID3D12Resource[] primitivesCB;

        private long mTlasSize = 0;
        private CpuDescriptorHandle indexSRVHandle;
        private CpuDescriptorHandle vertexSRVHandle;
        private CpuDescriptorHandle sceneCBVHandle;

        public Scene(Window window)
        {
            this.Window = window;
            this.context = new D3D12GraphicsContext(window.Width, window.Height);

            // InitDXR Tutorial 02
            this.InitDXR((IntPtr)window.Handle, window.Width, window.Height);

            // Acceleration Structures Tutorial 03
            this.CreateAccelerationStructures();

            // RtPipeline Tutorial 04
            this.CreateRtPipelineState();

            // ShaderResources Tutorial 06. Need to do this before initializing the shader-table
            this.CreateShaderResources();

            this.CreateConstantBuffer();

            // ShaderTable Tutorial 05
            this.CreateShaderTable();
        }

        private void InitDXR(IntPtr winHandle, int winWidth, int winHeight)
        {
            mHwnd = winHandle;
            this.mSwapChainRect = new Rect(0, 0, winWidth, winHeight);

            // Initialize the debug layer for debug builds
#if DEBUG
            if (D3D12.D3D12GetDebugInterface<ID3D12Debug>(out var pDx12Debug).Success)
            {
                pDx12Debug.EnableDebugLayer();
            }
#endif
            // Create the DXGI factory
            IDXGIFactory4 pDXGIFactory;
            DXGI.CreateDXGIFactory1<IDXGIFactory4>(out pDXGIFactory);
            mpDevice = this.context.CreateDevice(pDXGIFactory);
            mpCmdQueue = this.context.CreateCommandQueue(mpDevice);
            mpSwapChain = this.context.CreateDXGISwapChain(pDXGIFactory, mHwnd, winWidth, winHeight, Format.R8G8B8A8_UNorm, mpCmdQueue);

            // Create a RTV descriptor heap
            mRtvHeap.Heap = this.context.CreateDescriptorHeap(mpDevice, kRtvHeapSize, DescriptorHeapType.RenderTargetView, false);

            // Create the per-frame objects
            this.mFrameObjects = new FrameObject[this.context.kDefaultSwapChainBuffers];
            for (int i = 0; i < this.context.kDefaultSwapChainBuffers; i++)
            {
                mFrameObjects[i].pCmdAllocator = mpDevice.CreateCommandAllocator(CommandListType.Direct);
                mFrameObjects[i].pSwapChainBuffer = mpSwapChain.GetBuffer<ID3D12Resource>(i);
                mFrameObjects[i].rtvHandle = context.CreateRTV(mpDevice, mFrameObjects[i].pSwapChainBuffer, mRtvHeap.Heap, ref mRtvHeap.usedEntries, Format.R8G8B8A8_UNorm_SRgb);
            }

            // Create the command-list
            var cmdList = mpDevice.CreateCommandList(0, CommandListType.Direct, mFrameObjects[0].pCmdAllocator, null);
            this.mpCmdList = cmdList.QueryInterface<ID3D12GraphicsCommandList4>();

            // Create a fence and the event
            this.mpFence = mpDevice.CreateFence(0, FenceFlags.None);
            this.mFenceEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
        }

        public void CreateAccelerationStructures()
        {
            acs = new AccelerationStructures();

            AccelerationStructureBuffers[] bottomLevelBuffers = new AccelerationStructureBuffers[2];
            bottomLevelBuffers[0] = acs.CreatePlaneBottomLevelAS(mpDevice, mpCmdList);
            bottomLevelBuffers[1] = acs.CreatePrimitiveBottomLevelAS(mpDevice, mpCmdList);

            AccelerationStructureBuffers topLevelBuffers = acs.CreateTopLevelAS(mpDevice, mpCmdList, bottomLevelBuffers, ref mTlasSize);

            // The tutorial doesn't have any resource lifetime management, so we flush and sync here. This is not required by the DXR spec - you can submit the list whenever you like as long as you take care of the resources lifetime.
            mFenceValue = context.SubmitCommandList(mpCmdList, mpCmdQueue, mpFence, mFenceValue);
            mpFence.SetEventOnCompletion(mFenceValue, mFenceEvent);
            mFenceEvent.WaitOne();
            int bufferIndex = mpSwapChain.GetCurrentBackBufferIndex();
            mpCmdList.Reset(mFrameObjects[0].pCmdAllocator, null);

            // Store the AS buffers. The rest of the buffers will be released once we exit the function
            mpTopLevelAS = topLevelBuffers.pResult;
        }

        public void CreateRtPipelineState()
        {
            var rtpipeline = new RTPipeline();

            // Need 10 subobjects:
            //  1 for the DXIL library
            //  1 for hit-group
            //  2 for RayGen root-signature (root-signature and the subobject association)
            //  2 for hit-program root-signature (root-signature and the subobject association)
            //  2 for miss-shader root-signature (signature and association)
            //  2 for shader config (shared between all programs. 1 for the config, 1 for association)
            //  1 for pipeline config
            //  1 for the global root signature
            StateSubObject[] subobjects = new StateSubObject[12];
            int index = 0;

            // Create the DXIL library
            DxilLibrary dxilLib = rtpipeline.CreateDxilLibrary();
            subobjects[index++] = dxilLib.stateSubObject; // 0 Library

            HitProgram hitProgram = new HitProgram(null, RTPipeline.kClosestHitShader, RTPipeline.kHitGroup);
            subobjects[index++] = hitProgram.subObject; // 1 Hit Group

            // Create the ray-gen root-signature and association
            Structs.LocalRootSignature rgsRootSignature = new Structs.LocalRootSignature(mpDevice, rtpipeline.CreateRayGenRootDesc());
            subobjects[index] = rgsRootSignature.subobject; // 2 RayGen Root Sig

            int rgsRootIndex = index++; // 2
            ExportAssociation rgsRootAssociation = new ExportAssociation(new string[] { RTPipeline.kRayGenShader }, subobjects[rgsRootIndex]);
            subobjects[index++] = rgsRootAssociation.subobject; // 3 Associate Root Sig to RGS

            // Create the hit root-signature and association
            Structs.LocalRootSignature hitRootSignature = new Structs.LocalRootSignature(mpDevice, rtpipeline.CreateHitRootDesc());
            subobjects[index] = hitRootSignature.subobject; // 4 Hit Root Sig

            int hitRootIndex = index++; // 4
            ExportAssociation hitRootAssociation = new ExportAssociation(new string[] { RTPipeline.kClosestHitShader }, subobjects[hitRootIndex]); // 5 Associate Hit Root Sig to Hit Group
            subobjects[index++] = hitRootAssociation.subobject; // 6 Associate Hit Root Sig to Hit Group

            // Create the miss root-signature and association
            Structs.LocalRootSignature missRootSignature = new Structs.LocalRootSignature(mpDevice, rtpipeline.CreateMissRootDesc());
            subobjects[index] = missRootSignature.subobject; // 6 Miss Root Sig

            int missRootIndex = index++;  // 6
            ExportAssociation missRootAssociation = new ExportAssociation(new string[] { RTPipeline.kMissShader, RTPipeline.kShadowMiss }, subobjects[missRootIndex]);
            subobjects[index++] = missRootAssociation.subobject; // 7 Associate Miss Root Sig to Miss Shader

            // Bind the payload size to the programs
            ShaderConfig shaderConfig = new ShaderConfig(sizeof(float) * 2, sizeof(float) * (4 + 1)); //MaxPayloadSize float4 + uint
            subobjects[index] = shaderConfig.subObject; // 8 Shader Config;

            int shaderConfigIndex = index++; // 8
            string[] shaderExports = new string[] { RTPipeline.kMissShader, RTPipeline.kClosestHitShader, RTPipeline.kRayGenShader, RTPipeline.kShadowMiss };
            ExportAssociation configAssociation = new ExportAssociation(shaderExports, subobjects[shaderConfigIndex]);
            subobjects[index++] = configAssociation.subobject;  // 9 Associate Shader Config to Miss, CHS, RGS

            // Create the pipeline config
            PipelineConfig config = new PipelineConfig(4 + 1);
            subobjects[index++] = config.suboject; // 10

            // Create the global root signature and store the empty signature
            Structs.GlobalRootSignature root = new Structs.GlobalRootSignature(mpDevice, new RootSignatureDescription());
            mpEmptyRootSig = root.pRootSig.RootSignature;
            subobjects[index++] = root.suboject; // 11

            // Create the state
            StateObjectDescription desc = new StateObjectDescription(StateObjectType.RaytracingPipeline, subobjects);

            mpPipelineState = mpDevice.CreateStateObject(desc);
        }

        private const uint D3D12ShaderIdentifierSizeInBytes = 32;
        private const uint D3D12RaytracingShaderRecordByteAlignment = 32;

        private static uint align_to(uint _alignment, uint _val)
        {
            return (((_val + _alignment - 1) / _alignment) * _alignment);
        }

        public unsafe void CreateShaderTable()
        {
            /** The shader-table layout is as follows:
                Entry 0 - Ray-gen program
                Entry 1 - Miss program
                Entry 2 - Hit program
                Entry 3 - Hit program
                Entry 4 - Hit program
                All entries in the shader-table must have the same size, so we will choose it base on the largest required entry.
                The ray-gen program requires the largest entry - sizeof(program identifier) + 8 bytes for a descriptor-table.
                The entry size must be aligned up to D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT
            */

            // Calculate the size and create the buffer     
            mShaderTableEntrySize = D3D12ShaderIdentifierSizeInBytes;
            mShaderTableEntrySize += 8; // the ray-gen's descriptor table
            mShaderTableEntrySize = align_to(D3D12RaytracingShaderRecordByteAlignment, mShaderTableEntrySize);
            uint shaderTableSize = mShaderTableEntrySize * 5;

            // For simplicity, we create the shader.table on the upload heap. You can also create it on the default heap
            mpShaderTable = this.acs.CreateBuffer(mpDevice, shaderTableSize, ResourceFlags.None, ResourceStates.GenericRead, AccelerationStructures.kUploadHeapProps);

            // Map the buffer
            IntPtr pData;
            pData = mpShaderTable.Map(0, null);

            ID3D12StateObjectProperties pRtsoProps;
            pRtsoProps = mpPipelineState.QueryInterface<ID3D12StateObjectProperties>();

            // Entry 0 - ray-gen program ID and descriptor data
            Unsafe.CopyBlock((void*)pData, (void*)pRtsoProps.GetShaderIdentifier(RTPipeline.kRayGenShader), D3D12ShaderIdentifierSizeInBytes);

            ulong heapStart = (ulong)mpSrvUavHeap.GetGPUDescriptorHandleForHeapStart().Ptr;
            Unsafe.Write<ulong>((pData + (int)D3D12ShaderIdentifierSizeInBytes).ToPointer(), heapStart);

            // This is where we need to set the descriptor data for the ray-gen shader. We'll get to it in the next tutorial

            // Entry 1 - miss program
            pData += (int)mShaderTableEntrySize; // +1 skips ray-gen
            Unsafe.CopyBlock((void*)pData, (void*)pRtsoProps.GetShaderIdentifier(RTPipeline.kMissShader), D3D12ShaderIdentifierSizeInBytes);

            // Entry 2 - miss program
            pData += (int)mShaderTableEntrySize; // +1 skips ray-gen
            Unsafe.CopyBlock((void*)pData, (void*)pRtsoProps.GetShaderIdentifier(RTPipeline.kShadowMiss), D3D12ShaderIdentifierSizeInBytes);

            // Entry 3-5 - hit program
            //heapStart = (ulong)mpSrvUavHeap.GetGPUDescriptorHandleForHeapStart().Ptr;                
            for (int i = 0; i < 6; i++)
            {
                pData += (int)mShaderTableEntrySize; // +1 skips miss entries
                Unsafe.CopyBlock((void*)pData, (void*)pRtsoProps.GetShaderIdentifier(RTPipeline.kHitGroup), D3D12ShaderIdentifierSizeInBytes);

                Unsafe.Write<ulong>((pData + (int)D3D12ShaderIdentifierSizeInBytes).ToPointer(), heapStart);

                Unsafe.Write<ulong>((pData + (int)(D3D12ShaderIdentifierSizeInBytes + sizeof(ulong))).ToPointer(), (ulong)primitivesCB[i].GPUVirtualAddress);
            }

            // Unmap
            mpShaderTable.Unmap(0, null);
        }

        public void CreateShaderResources()
        {
            // Create the output resource. The dimensions and format should match the swap-chain
            ResourceDescription resDesc = new ResourceDescription();
            resDesc.DepthOrArraySize = 1;
            resDesc.Dimension = ResourceDimension.Texture2D;
            resDesc.Format = Format.R8G8B8A8_UNorm; // The backbuffer is actually DXGI_FORMAT_R8G8B8A8_UNORM_SRGB, but sRGB formats can't be used with UAVs. We will convert to sRGB ourselves in the shader
            resDesc.Flags = ResourceFlags.AllowUnorderedAccess;
            resDesc.Height = mSwapChainRect.Height;
            resDesc.Layout = TextureLayout.Unknown;
            resDesc.MipLevels = 1;
            resDesc.SampleDescription = new SampleDescription(1, 0);
            resDesc.Width = mSwapChainRect.Width;
            mpOutputResource = mpDevice.CreateCommittedResource(AccelerationStructures.kDefaultHeapProps, HeapFlags.None, resDesc, ResourceStates.CopySource, null);  // Starting as copy-source to simplify onFrameRender()

            // Create an SRV/UAV/VertexSRV/IndexSRV descriptor heap. Need 5 entries - 1 SRV for the scene, 1 UAV for the output, 1 SRV for VertexBuffer, 1 SRV for IndexBuffer, 1 SceneContantBuffer, 1 primitiveConstantBuffer
            mpSrvUavHeap = this.context.CreateDescriptorHeap(mpDevice, 5, DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, true);

            // Create the UAV. Based on the root signature we created it should be the first entry
            UnorderedAccessViewDescription uavDesc = new UnorderedAccessViewDescription();
            uavDesc.ViewDimension = UnorderedAccessViewDimension.Texture2D;
            mpDevice.CreateUnorderedAccessView(mpOutputResource, null, uavDesc, mpSrvUavHeap.GetCPUDescriptorHandleForHeapStart());

            // Create the TLAS SRV right after the UAV. Note that we are using a different SRV desc here
            ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription();
            srvDesc.ViewDimension = ShaderResourceViewDimension.RaytracingAccelerationStructure;
            srvDesc.Shader4ComponentMapping = D3D12DefaultShader4ComponentMapping;
            srvDesc.RaytracingAccelerationStructure = new RaytracingAccelerationStructureShaderResourceView();
            srvDesc.RaytracingAccelerationStructure.Location = mpTopLevelAS.GPUVirtualAddress;
            CpuDescriptorHandle srvHandle = mpSrvUavHeap.GetCPUDescriptorHandleForHeapStart();
            srvHandle.Ptr += mpDevice.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);
            mpDevice.CreateShaderResourceView(null, srvDesc, srvHandle);

            // Index SRV
            var indexSRVDesc = new ShaderResourceViewDescription()
            {
                ViewDimension = ShaderResourceViewDimension.Buffer,
                Shader4ComponentMapping = D3D12DefaultShader4ComponentMapping,
                Format = Format.R32_Typeless,
                Buffer =
                {
                    NumElements = (int)(this.acs.IndexCount * 2 / 4),
                    Flags = BufferShaderResourceViewFlags.Raw,
                    StructureByteStride = 0,
                }
            };

            srvHandle.Ptr += mpDevice.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);
            indexSRVHandle = srvHandle;
            mpDevice.CreateShaderResourceView(this.acs.IndexBuffer, indexSRVDesc, indexSRVHandle);

            // Vertex SRV            
            var vertexSRVDesc = new ShaderResourceViewDescription()
            {
                ViewDimension = ShaderResourceViewDimension.Buffer,
                Shader4ComponentMapping = D3D12DefaultShader4ComponentMapping,
                Format = Format.Unknown,
                Buffer =
                {
                    NumElements = (int)this.acs.VertexCount,
                    Flags = BufferShaderResourceViewFlags.None,
                    StructureByteStride  = Unsafe.SizeOf<VertexPositionNormalTangentTexture>(),
                }
            };
            srvHandle.Ptr += mpDevice.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);
            vertexSRVHandle = srvHandle;
            mpDevice.CreateShaderResourceView(this.acs.VertexBuffer, vertexSRVDesc, vertexSRVHandle);

            // CB Scene
            Vector3 cameraPosition = new Vector3(0, 1, -7);
            Matrix4x4 view = Matrix4x4.CreateLookAt(cameraPosition, Vector3.Zero, Vector3.UnitY);
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, (float)mSwapChainRect.Width / mSwapChainRect.Height, 0.1f, 1000f);
            Matrix4x4 viewProj = Matrix4x4.Multiply(view, proj);
            Matrix4x4.Invert(viewProj, out Matrix4x4 projectionToWorld);
            SceneConstantBuffer sceneConstantBuffer = new SceneConstantBuffer()
            {
                projectionToWorld = Matrix4x4.Transpose(projectionToWorld),
                cameraPosition = cameraPosition,
                lightPosition = new Vector3(0.0f, 1.0f, -2.0f),
                lightDiffuseColor = new Vector4(0.5f, 0.5f, 0.5f, 1.0f),
                lightAmbientColor = new Vector4(0.1f, 0.1f, 0.1f, 1.0f),
                backgroundColor = new Vector4(0.2f, 0.21f, 0.9f, 1.0f),
                MaxRecursionDepth = 4,
            };

            sceneCB = this.acs.CreateBuffer(mpDevice, (uint)Unsafe.SizeOf<SceneConstantBuffer>(), ResourceFlags.None, ResourceStates.GenericRead, AccelerationStructures.kUploadHeapProps);
            IntPtr pData;
            pData = sceneCB.Map(0, null);
            Helpers.MemCpy(pData, sceneConstantBuffer, (uint)Unsafe.SizeOf<SceneConstantBuffer>());
            sceneCB.Unmap(0, null);

            var sceneCBV = new ConstantBufferViewDescription()
            {
                BufferLocation = sceneCB.GPUVirtualAddress,
                SizeInBytes = (Unsafe.SizeOf<SceneConstantBuffer>() + 255) & ~255,
            };

            srvHandle.Ptr += mpDevice.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);
            sceneCBVHandle = srvHandle;
            mpDevice.CreateConstantBufferView(sceneCBV, sceneCBVHandle);           
        }
       
        public unsafe void CreateConstantBuffer()
        {
            int instances = 6;
            PrimitiveConstantBuffer[] primitiveConstantBuffer = new PrimitiveConstantBuffer[instances];
            primitiveConstantBuffer[0] = new PrimitiveConstantBuffer()
            {
                diffuseColor = new Vector4(0.9f, 0.9f, 0.9f, 1.0f),
                inShadowRadiance = 0.35f,
                diffuseCoef = 0.9f,
                specularCoef = 0.7f,
                specularPower = 50,
                reflectanceCoef = 0.7f,
            };
            primitiveConstantBuffer[1] = new PrimitiveConstantBuffer()
            {
                diffuseColor = new Vector4(0.6f, 0.1f, 0.1f, 1.0f),
                inShadowRadiance = 0.35f,
                diffuseCoef = 0.1f,
                specularCoef = 0.7f,
                specularPower = 50,
                reflectanceCoef = 0.7f,
            };
            primitiveConstantBuffer[2] = new PrimitiveConstantBuffer()
            {
                diffuseColor = new Vector4(0.2f, 0.6f, 0.2f, 1.0f),
                inShadowRadiance = 0.35f,
                diffuseCoef = 0.1f,
                specularCoef = 0.7f,
                specularPower = 50,
                reflectanceCoef = 0.7f,
            };
            primitiveConstantBuffer[3] = new PrimitiveConstantBuffer()
            {
                diffuseColor = new Vector4(0.6f, 0.2f, 0.6f, 1.0f),
                inShadowRadiance = 0.35f,
                diffuseCoef = 0.1f,
                specularCoef = 0.7f,
                specularPower = 50,
                reflectanceCoef = 0.7f,
            };
            primitiveConstantBuffer[4] = new PrimitiveConstantBuffer()
            {
                diffuseColor = new Vector4(0.6f, 0.6f, 0f, 1.0f),
                inShadowRadiance = 0.35f,
                diffuseCoef = 0.1f,
                specularCoef = 0.7f,
                specularPower = 50,
                reflectanceCoef = 0.7f,
            };
            primitiveConstantBuffer[5] = new PrimitiveConstantBuffer()
            {
                diffuseColor = new Vector4(0.0f, 0.6f, 0.6f, 1.0f),
                inShadowRadiance = 0.35f,
                diffuseCoef = 0.1f,
                specularCoef = 0.7f,
                specularPower = 50,
                reflectanceCoef = 0.7f,
            };

            this.primitivesCB = new ID3D12Resource[instances];
            for (int i = 0; i < this.primitivesCB.Length; i++)
            {
                uint bufferSize = (uint)Unsafe.SizeOf<PrimitiveConstantBuffer>();
                this.primitivesCB[i] = this.acs.CreateBuffer(mpDevice, bufferSize, ResourceFlags.None, ResourceStates.GenericRead, AccelerationStructures.kUploadHeapProps);
                IntPtr pData;
                pData = this.primitivesCB[i].Map(0, null);
                fixed (void* pSource = &primitiveConstantBuffer[i])
                {
                    Unsafe.CopyBlock((void*)pData, pSource, bufferSize);
                }                
                this.primitivesCB[i].Unmap(0, null);
            }
        }

        private int BeginFrame()
        {
            // Bind the descriptor heaps
            ID3D12DescriptorHeap[] heaps = new ID3D12DescriptorHeap[] { mpSrvUavHeap };
            mpCmdList.SetDescriptorHeaps(1, heaps);

            return this.mpSwapChain.GetCurrentBackBufferIndex();
        }

        public bool DrawFrame(Action<int, int> draw, [CallerMemberName] string frameName = null)
        {
            int rtvIndex = BeginFrame();

            // Let's raytrace
            context.ResourceBarrier(mpCmdList, mpOutputResource, ResourceStates.CopySource, ResourceStates.UnorderedAccess);
            DispatchRaysDescription raytraceDesc = new DispatchRaysDescription();
            raytraceDesc.Width = mSwapChainRect.Width;
            raytraceDesc.Height = mSwapChainRect.Height;
            raytraceDesc.Depth = 1;

            // RayGen is the first entry in the shader-table
            raytraceDesc.RayGenerationShaderRecord.StartAddress = mpShaderTable.GPUVirtualAddress + 0 * mShaderTableEntrySize;
            raytraceDesc.RayGenerationShaderRecord.SizeInBytes = mShaderTableEntrySize;

            // Miss is the second entry in the shader-table
            uint missOffset = 1 * mShaderTableEntrySize;
            raytraceDesc.MissShaderTable.StartAddress = mpShaderTable.GPUVirtualAddress + missOffset;
            raytraceDesc.MissShaderTable.StrideInBytes = mShaderTableEntrySize;
            raytraceDesc.MissShaderTable.SizeInBytes = mShaderTableEntrySize * 2; // Only a s single miss-entry 

            // Hit is the third entry in the shader-table
            uint hitOffset = 3 * mShaderTableEntrySize;
            raytraceDesc.HitGroupTable.StartAddress = mpShaderTable.GPUVirtualAddress + hitOffset;
            raytraceDesc.HitGroupTable.StrideInBytes = mShaderTableEntrySize;
            raytraceDesc.HitGroupTable.SizeInBytes = mShaderTableEntrySize*2;

            // Bind the empty root signature
            mpCmdList.SetComputeRootSignature(mpEmptyRootSig);

            // Dispatch
            mpCmdList.SetPipelineState1(mpPipelineState);
            mpCmdList.DispatchRays(raytraceDesc);

            // Copy the results to the back-buffer
            context.ResourceBarrier(mpCmdList, mpOutputResource, ResourceStates.UnorderedAccess, ResourceStates.CopySource);
            context.ResourceBarrier(mpCmdList, mFrameObjects[rtvIndex].pSwapChainBuffer, ResourceStates.Present, ResourceStates.CopyDestination);
            mpCmdList.CopyResource(mFrameObjects[rtvIndex].pSwapChainBuffer, mpOutputResource);

            EndFrame(rtvIndex);

            return true;
        }

        private void EndFrame(int rtvIndex)
        {
            context.ResourceBarrier(mpCmdList, mFrameObjects[rtvIndex].pSwapChainBuffer, ResourceStates.CopyDestination, ResourceStates.Present);
            mFenceValue = context.SubmitCommandList(mpCmdList, mpCmdQueue, mpFence, mFenceValue);
            mpSwapChain.Present(0, 0);

            // Prepare the command list for the next frame
            int bufferIndex = mpSwapChain.GetCurrentBackBufferIndex();

            // Make sure we have the new back-buffer is ready
            if (mFenceValue > context.kDefaultSwapChainBuffers)
            {
                mpFence.SetEventOnCompletion(mFenceValue - context.kDefaultSwapChainBuffers + 1, mFenceEvent);
                this.mFenceEvent.WaitOne();
            }

            mFrameObjects[bufferIndex].pCmdAllocator.Reset();
            mpCmdList.Reset(mFrameObjects[bufferIndex].pCmdAllocator, null);
        }

        public void Dispose()
        {
            mFenceValue++;
            mpCmdQueue.Signal(mpFence, mFenceValue);
            mpFence.SetEventOnCompletion(mFenceValue, mFenceEvent);
            mFenceEvent.WaitOne();
        }
    }
}

using RayTracingTutorial04.RTX;
using RayTracingTutorial04.Structs;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace RayTracingTutorial04
{
    public class Scene
    {
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
        private ID3D12Resource mpBottomLevelAS;
        private ID3D12StateObject mpPipelineState;
        private Vortice.Direct3D12.GlobalRootSignature mpEmptyRootSig;

        public Scene(Window window)
        {
            this.Window = window;
            this.context = new D3D12GraphicsContext(window.Width, window.Height);            

            // InitDXR
            this.InitDXR((IntPtr)window.Handle, window.Width, window.Height);

            // Acceleration Structures            
            this.CreateAccelerationStructures();

            // RtPipeline
            this.CreateRtPipelineState();
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
                mFrameObjects[i].swapChainBuffer = mpSwapChain.GetBuffer<ID3D12Resource>(i);
                mFrameObjects[i].rtvHandle = context.CreateRTV(mpDevice, mFrameObjects[i].swapChainBuffer, mRtvHeap.Heap, ref mRtvHeap.usedEntries, Format.R8G8B8A8_UNorm_SRgb);
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
            var acs = new AccelerationStructures();

            long mTlasSize = 0;

            var mpVertexBuffer = acs.CreateTriangleVB(mpDevice);
            AccelerationStructureBuffers bottomLevelBuffers = acs.CreateBottomLevelAS(mpDevice, mpCmdList, mpVertexBuffer);
            AccelerationStructureBuffers topLevelBuffers = acs.CreateTopLevelAS(mpDevice, mpCmdList, bottomLevelBuffers.pResult, ref mTlasSize);
            
            // The tutorial doesn't have any resource lifetime management, so we flush and sync here. This is not required by the DXR spec - you can submit the list whenever you like as long as you take care of the resources lifetime.
            mFenceValue = context.SubmitCommandList(mpCmdList, mpCmdQueue, mpFence, mFenceValue);
            mpFence.SetEventOnCompletion(mFenceValue, mFenceEvent);
            mFenceEvent.WaitOne();
            int bufferIndex = mpSwapChain.GetCurrentBackBufferIndex();
            mpCmdList.Reset(mFrameObjects[0].pCmdAllocator, null);

            // Store the AS buffers. The rest of the buffers will be released once we exit the function
            mpTopLevelAS = topLevelBuffers.pResult;
            mpBottomLevelAS = bottomLevelBuffers.pResult;
        }

        public void CreateRtPipelineState()
        {
            var rtpipeline = new RTPipeline();

            // Need 10 subobjects:
            //  1 for the DXIL library
            //  1 for hit-group
            //  2 for RayGen root-signature (root-signature and the subobject association)
            //  2 for the root-signature shared between miss and hit shaders (signature and association)
            //  2 for shader config (shared between all programs. 1 for the config, 1 for association)
            //  1 for pipeline config
            //  1 for the global root signature
            StateSubObject[] subobjects = new StateSubObject[10];
            int index = 0;

            // Create the DXIL library
            DxilLibrary dxilLib = rtpipeline.CreateDxilLibrary();
            subobjects[index++] = dxilLib.stateSubObject; // 0 Library

            HitProgram hitProgram = new HitProgram(null, RTPipeline.kClosestHitShader, RTPipeline.kHitGroup);
            subobjects[index++] = hitProgram.subObject; // 1 Hit Group

            // Create the ray-gen root-signature and association
            Structs.LocalRootSignature rgsRootSignature = new Structs.LocalRootSignature(mpDevice, rtpipeline.CreateRayGenRootDesc());
            subobjects[index] = rgsRootSignature.subobject;

            int rgsRootIndex = index++; // 2
            ExportAssociation rgsRootAssociation = new ExportAssociation(new string[] { RTPipeline.kRayGenShader }, subobjects[rgsRootIndex]);
            subobjects[index++] = rgsRootAssociation.subobject; // 3 Associate Root Sig to RGS

            // Create the miss- and hit-programs root-signature and association
            RootSignatureDescription emptyDesc = new RootSignatureDescription(RootSignatureFlags.LocalRootSignature);
            Structs.LocalRootSignature hitMissRootSignature = new Structs.LocalRootSignature(mpDevice, emptyDesc);
            subobjects[index] = hitMissRootSignature.subobject; // 4 Root Sig to be shared between Miss and CHS

            int hitMissRootIndex = index++; // 4
            string[] missHitExportName = new string[] { RTPipeline.kMissShader, RTPipeline.kClosestHitShader };
            ExportAssociation missHitRootAssociation = new ExportAssociation(missHitExportName, subobjects[hitMissRootIndex]);
            subobjects[index++] = missHitRootAssociation.subobject; // 5 Associate Root Sig to Miss and CHS

            // Bind the payload size to the programs
            ShaderConfig shaderConfig = new ShaderConfig(sizeof(float) * 2, sizeof(float) * 1);
            subobjects[index] = shaderConfig.subObject; // 6 Shader Config;

            int shaderConfigIndex = index++; // 6
            string[] shaderExports = new string[] { RTPipeline.kMissShader, RTPipeline.kClosestHitShader, RTPipeline.kRayGenShader };
            ExportAssociation configAssociation = new ExportAssociation(shaderExports, subobjects[shaderConfigIndex]);
            subobjects[index++] = configAssociation.subobject;  // 7 Associate Shader Config to Miss, CHS, RGS

            // Create the pipeline config
            PipelineConfig config = new PipelineConfig(0);
            subobjects[index++] = config.suboject; // 8

            // Create the global root signature and store the empty signature
            Structs.GlobalRootSignature root = new Structs.GlobalRootSignature(mpDevice, new RootSignatureDescription());
            mpEmptyRootSig = root.pRootSig;
            subobjects[index++] = root.suboject; // 9
            
            // Create the state
            StateObjectDescription desc = new StateObjectDescription(StateObjectType.RaytracingPipeline, subobjects);

            mpPipelineState = mpDevice.CreateStateObject(desc);
        }

        private int BeginFrame()
        {
            return this.mpSwapChain.GetCurrentBackBufferIndex();
        }

        public bool DrawFrame(Action<int, int> draw, [CallerMemberName] string frameName = null)
        {
            int rtvIndex = BeginFrame();

            context.ResourceBarrier(mpCmdList, mFrameObjects[rtvIndex].swapChainBuffer, ResourceStates.Present, ResourceStates.RenderTarget);
            mpCmdList.ClearRenderTargetView(mFrameObjects[rtvIndex].rtvHandle, clearColor, mSwapChainRect);

            EndFrame(rtvIndex);

            return true;
        }

        private void EndFrame(int rtvIndex)
        {
            context.ResourceBarrier(mpCmdList, mFrameObjects[rtvIndex].swapChainBuffer, ResourceStates.RenderTarget, ResourceStates.Present);
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

using RayTracingTutorial04.Structs;
using System;
using System.Threading;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace RayTracingTutorial04.RTX
{
    public class D3D12GraphicsContext
    {
        public int kDefaultSwapChainBuffers = 2;

        public D3D12GraphicsContext(int winWidth, int winHeight)
        {
            if (!D3D12.IsSupported(Vortice.Direct3D.FeatureLevel.Level_12_0))
            {
                throw new InvalidOperationException("Direct3D12 is not supported on current OS");
            }
        }

        public IDXGISwapChain3 CreateDXGISwapChain(IDXGIFactory4 pFactory, IntPtr hwnd, int width, int height, Vortice.DXGI.Format format, ID3D12CommandQueue pCommandQueue)
        {
            SwapChainDescription1 swapChainDesc = new SwapChainDescription1();
            swapChainDesc.BufferCount = kDefaultSwapChainBuffers;
            swapChainDesc.Width = width;
            swapChainDesc.Height = height;
            swapChainDesc.Format = format;
            swapChainDesc.Usage = Usage.RenderTargetOutput;
            swapChainDesc.SwapEffect = SwapEffect.FlipDiscard;
            swapChainDesc.SampleDescription = new SampleDescription(1, 0);

            // CreateSwapChainForHwnd() doesn't accept IDXGISwapChain3 (Why MS? Why?)
            IDXGISwapChain1 pSwapChain = pFactory.CreateSwapChainForHwnd(pCommandQueue, hwnd, swapChainDesc);
            IDXGISwapChain3 pSwapChain3 = pSwapChain.QueryInterface<IDXGISwapChain3>();
            return pSwapChain3;
        }

        public ID3D12Device5 CreateDevice(IDXGIFactory4 pDxgiFactory)
        {
            // Find the HW adapter
            IDXGIAdapter1 pAdapter;

            var adapters = pDxgiFactory.EnumAdapters1();
            for (uint i = 0; i < adapters.Length; i++)
            {
                pAdapter = adapters[i];
                AdapterDescription1 desc = pAdapter.Description1;

                // Skip SW adapters
                if (desc.Flags.HasFlag(AdapterFlags.Software)) continue;
#if DEBUG
                if (D3D12.D3D12GetDebugInterface<ID3D12Debug>(out var pDx12Debug).Success)
                {
                    pDx12Debug.EnableDebugLayer();
                }
#endif
                var res = D3D12.D3D12CreateDevice(pAdapter, Vortice.Direct3D.FeatureLevel.Level_12_0, out ID3D12Device pDevice);
                FeatureDataD3D12Options5 features5 = pDevice.CheckFeatureSupport<FeatureDataD3D12Options5>(Vortice.Direct3D12.Feature.Options5);
                if (features5.RaytracingTier == RaytracingTier.NotSupported)
                {
                    throw new NotSupportedException("Raytracing is not supported on this device.Make sure your GPU supports DXR(such as Nvidia's Volta or Turing RTX) and you're on the latest drivers.The DXR fallback layer is not supported.");
                }

                return pDevice.QueryInterface<ID3D12Device5>();
            }

            return null;
        }

        public ID3D12CommandQueue CreateCommandQueue(ID3D12Device5 pDevice)
        {
            ID3D12CommandQueue pQueue;
            CommandQueueDescription cpDesc = new CommandQueueDescription();
            cpDesc.Flags = CommandQueueFlags.None;
            cpDesc.Type = CommandListType.Direct;
            pQueue = pDevice.CreateCommandQueue(cpDesc);

            return pQueue;
        }

        public ID3D12DescriptorHeap CreateDescriptorHeap(ID3D12Device5 pDevice, int count, DescriptorHeapType type, bool shaderVisible)
        {
            DescriptorHeapDescription desc = new DescriptorHeapDescription();
            desc.DescriptorCount = count;
            desc.Type = type;
            desc.Flags = shaderVisible ? DescriptorHeapFlags.ShaderVisible : DescriptorHeapFlags.None;

            ID3D12DescriptorHeap pHeap;
            pHeap = pDevice.CreateDescriptorHeap(desc);

            return pHeap;
        }

        public CpuDescriptorHandle CreateRTV(ID3D12Device5 pDevice, ID3D12Resource pResource, ID3D12DescriptorHeap pHeap, ref uint usedHeapEntries, Format format)
        {
            RenderTargetViewDescription desc = new RenderTargetViewDescription();
            desc.ViewDimension = RenderTargetViewDimension.Texture2D;
            desc.Format = format;
            desc.Texture2D = new Texture2DRenderTargetView();
            desc.Texture2D.MipSlice = 0;
            CpuDescriptorHandle rtvHandle = pHeap.GetCPUDescriptorHandleForHeapStart();
            rtvHandle.Ptr += usedHeapEntries * pDevice.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);
            usedHeapEntries++;
            pDevice.CreateRenderTargetView(pResource, desc, rtvHandle);
            return rtvHandle;
        }

        public void ResourceBarrier(ID3D12GraphicsCommandList4 pCmdLit, ID3D12Resource pResource, ResourceStates stateBefore, ResourceStates stateAfter)
        {
            ResourceBarrier barrier = new ResourceBarrier(new ResourceTransitionBarrier(pResource, stateBefore, stateAfter));
            pCmdLit.ResourceBarrier(barrier);
        }

        public uint SubmitCommandList(ID3D12GraphicsCommandList4 pCmdList, ID3D12CommandQueue pCmdQueue, ID3D12Fence pFence, uint fenceValue)
        {
            pCmdList.Close();
            pCmdQueue.ExecuteCommandList(pCmdList);
            fenceValue++;
            pCmdQueue.Signal(pFence, fenceValue);
            return fenceValue;
        }
    }
}

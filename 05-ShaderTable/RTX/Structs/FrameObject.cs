using Vortice.Direct3D12;

namespace RayTracingTutorial05.Structs
{
    internal struct FrameObject
    {
        public ID3D12CommandAllocator pCmdAllocator;
        public ID3D12Resource swapChainBuffer;
        public CpuDescriptorHandle rtvHandle;
    };
}

using Vortice.Direct3D12;

namespace RayTracingTutorial08.Structs
{
    internal struct HeapData
    {
        public ID3D12DescriptorHeap Heap;
        public uint usedEntries;
    };
}

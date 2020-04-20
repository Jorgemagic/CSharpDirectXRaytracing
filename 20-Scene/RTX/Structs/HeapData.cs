using Vortice.Direct3D12;

namespace RayTracingTutorial19.Structs
{
    internal struct HeapData
    {
        public ID3D12DescriptorHeap Heap;
        public uint usedEntries;
    };
}

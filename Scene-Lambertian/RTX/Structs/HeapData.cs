using Vortice.Direct3D12;

namespace SceneLambertian.Structs
{
    internal struct HeapData
    {
        public ID3D12DescriptorHeap Heap;
        public uint usedEntries;
    };
}

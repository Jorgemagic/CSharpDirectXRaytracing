﻿using Vortice.Direct3D12;

namespace RayTracingTutorial21.Structs
{
    internal struct HeapData
    {
        public ID3D12DescriptorHeap Heap;
        public uint usedEntries;
    };
}

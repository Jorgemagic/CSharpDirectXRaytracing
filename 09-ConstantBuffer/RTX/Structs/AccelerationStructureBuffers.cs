using Vortice.Direct3D12;

namespace RayTracingTutorial09.Structs
{
    public struct AccelerationStructureBuffers
    {
        public ID3D12Resource pScratch;
        public ID3D12Resource pResult;
        public ID3D12Resource pInstanceDesc;  // Used only for top-level AS
    }
}

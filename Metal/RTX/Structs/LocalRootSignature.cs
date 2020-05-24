using Vortice.Direct3D12;

namespace RayTracingTutorial24.Structs
{
    public struct LocalRootSignature
    {
        public Vortice.Direct3D12.LocalRootSignature pRootSig;
        public StateSubObject subobject;

        public LocalRootSignature(ID3D12Device5 pDevice, RootSignatureDescription desc)
        {
            pRootSig = new Vortice.Direct3D12.LocalRootSignature();
            pRootSig.RootSignature = pDevice.CreateRootSignature(desc, RootSignatureVersion.Version1);
            subobject = new StateSubObject(pRootSig);
        }
    }
}

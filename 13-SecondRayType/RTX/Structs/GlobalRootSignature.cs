using Vortice.Direct3D12;

namespace RayTracingTutorial13.Structs
{
    public struct GlobalRootSignature
    {
        public Vortice.Direct3D12.GlobalRootSignature pRootSig;
        public StateSubObject suboject;

        public GlobalRootSignature(ID3D12Device5 pDevice, RootSignatureDescription desc)
        {
            pRootSig = new Vortice.Direct3D12.GlobalRootSignature();
            pRootSig.RootSignature = pDevice.CreateRootSignature(desc, RootSignatureVersion.Version1);
            suboject = new StateSubObject(pRootSig);
        }
    }
}

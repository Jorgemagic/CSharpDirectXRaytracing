using Vortice.Direct3D12;

namespace RayTracingTutorial22.Structs
{
    public struct HitProgram
    {
        public StateSubObject subObject;

        public HitProgram(string ahsExport, string chsExport, string name)
        {
            HitGroupDescription desc = new HitGroupDescription();
            desc.AnyHitShaderImport = ahsExport;
            desc.ClosestHitShaderImport = chsExport;
            desc.HitGroupExport = name;

            subObject = new StateSubObject(desc);
        }
    }
}

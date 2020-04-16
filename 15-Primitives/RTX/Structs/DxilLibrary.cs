using Vortice.Direct3D12;
using Vortice.Dxc;

namespace RayTracingTutorial15.Structs
{
    public class DxilLibrary
    {
        public StateSubObject stateSubObject;

        public DxilLibrary(IDxcBlob pBlob, string[] entryPoint)
        {
            var pShaderBytecode = Dxc.GetBytesFromBlob(pBlob);

            ExportDescription[] exportDesc = new ExportDescription[entryPoint.Length];
            
            for (int i = 0; i < exportDesc.Length; i++)
            {
                exportDesc[i] = new ExportDescription();
                exportDesc[i].Name = entryPoint[i];
                exportDesc[i].Flags = ExportFlags.None;
                exportDesc[i].ExportToRename = null;
            }

            DxilLibraryDescription dxilLibDesc = new DxilLibraryDescription(new ShaderBytecode(pShaderBytecode), exportDesc);
            this.stateSubObject = new StateSubObject(dxilLibDesc);
        }
    }
}

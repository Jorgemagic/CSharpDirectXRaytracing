using RayTracingTutorial14.Structs;
using System.Diagnostics;
using System.IO;
using Vortice.Direct3D12;
using Vortice.Dxc;

namespace RayTracingTutorial14.RTX
{
    public class RTPipeline
    {
        public IDxcBlob CompileLibrary(string filename, DxcShaderModel targetString)
        {
            // Open and read the file
            string pTextBlob = File.ReadAllText(filename);

            // Compile
            var pResult = DxcCompiler.Compile(DxcShaderStage.Library, pTextBlob, string.Empty, string.Empty, new DxcCompilerOptions()
            {
                ShaderModel = targetString
            });

            // Verify the result
            int resultCode;
            resultCode = pResult.GetStatus();
            if (resultCode != 0)
            {
                Debug.WriteLine(pResult.GetStatus());
            }

            IDxcBlob pBlob;
            pBlob = pResult.GetResult();
            return pBlob;
        }

        public RootSignatureDescription CreateRayGenRootDesc()
        {
            // Create the root-signature
            RootSignatureDescription desc = new RootSignatureDescription();
            var descRange = new DescriptorRange[2];

            // gOutput
            descRange[0].BaseShaderRegister = 0;
            descRange[0].NumDescriptors = 1;
            descRange[0].RegisterSpace = 0;
            descRange[0].RangeType = DescriptorRangeType.UnorderedAccessView;
            descRange[0].OffsetInDescriptorsFromTableStart = 0;

            // gRTScene
            descRange[1].BaseShaderRegister = 0;
            descRange[1].NumDescriptors = 1;
            descRange[1].RegisterSpace = 0;
            descRange[1].RangeType = DescriptorRangeType.ShaderResourceView;
            descRange[1].OffsetInDescriptorsFromTableStart = 1;

            desc.Parameters = new RootParameter[1];
            desc.Parameters[0] = new RootParameter(
            new RootDescriptorTable(descRange),
            ShaderVisibility.All);

            desc.Flags = RootSignatureFlags.LocalRootSignature;
            return desc;
        }

        public RootSignatureDescription CreateTriangleHitRootDesc()
        {
            RootSignatureDescription desc = new RootSignatureDescription(RootSignatureFlags.LocalRootSignature,
                new RootParameter[]
                {
                    new RootParameter(RootParameterType.ConstantBufferView, new RootDescriptor(0, 0), ShaderVisibility.All),
                });

            return desc;
        }

        public RootSignatureDescription CreatePlaneHitRootDesc()
        {
            RootSignatureDescription desc = new RootSignatureDescription(RootSignatureFlags.LocalRootSignature,
               new RootParameter[]
               {
                    new RootParameter(new RootDescriptorTable(new DescriptorRange[]
                    {
                        new DescriptorRange(DescriptorRangeType.ShaderResourceView, 1, 0, 0, 0)
                    }), ShaderVisibility.All)
               });

            return desc;
        }

        public static string kRayGenShader = "rayGen";
        public static string kMissShader = "miss";
        public static string kTriangleChs = "triangleChs";
        public static string kPlaneChs = "planeChs";
        public static string kTriHitGroup = "TriHitGroup";
        public static string kPlaneHitGroup = "PlaneHitGroup";
        public static string kShadowChs = "shadowChs";
        public static string kShadowMiss = "shadowMiss";
        public static string kShadowHitGroup = "ShadowHitGroup";

        public DxilLibrary CreateDxilLibrary()
        {
            // Compile the shader
            IDxcBlob pDxilLib = this.CompileLibrary("Data/14-Shaders.hlsl", new DxcShaderModel(6, 3));
            string[] entryPoints = new string[] { kRayGenShader, kMissShader, kPlaneChs, kTriangleChs, kShadowMiss, kShadowChs };
            return new DxilLibrary(pDxilLib, entryPoints);
        }
    }
}

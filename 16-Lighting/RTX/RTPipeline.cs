using RayTracingTutorial16.Structs;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;
using Vortice.Dxc;

namespace RayTracingTutorial16.RTX
{
    public class RTPipeline
    {
        public unsafe IDxcBlob CompileLibrary(string filename, DxcShaderModel targetString)
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
                var error = pResult.GetErrors();
                string message = Marshal.PtrToStringAnsi((IntPtr)error.GetBufferPointer(), (int)error.GetBufferSize());
                Debug.WriteLine(message);
            }

            IDxcBlob pBlob;
            pBlob = pResult.GetResult();
            return pBlob;
        }

        public RootSignatureDescription CreateRayGenRootDesc()
        {
            // Create the root-signature
            RootSignatureDescription desc = new RootSignatureDescription(RootSignatureFlags.LocalRootSignature,
                new RootParameter[]
                {
                    new RootParameter(new RootDescriptorTable(new DescriptorRange[]
                    {
                        // gOutput
                        new DescriptorRange(DescriptorRangeType.UnorderedAccessView, 1, 0, 0, 0),
                        // gRtScene
                        new DescriptorRange(DescriptorRangeType.ShaderResourceView, 1, 0, 0, 1),                      
                    }), ShaderVisibility.All)
                });
            return desc;
        }

        public RootSignatureDescription CreateHitRootDesc()
        {
            RootSignatureDescription desc = new RootSignatureDescription(RootSignatureFlags.LocalRootSignature,
              new RootParameter[]
              {
                   new RootParameter(new RootDescriptorTable(new DescriptorRange[]
                    {
                         // Indices
                        new DescriptorRange(DescriptorRangeType.ShaderResourceView, 1, 1, 0, 2),
                        // Vertices
                        new DescriptorRange(DescriptorRangeType.ShaderResourceView, 1, 2, 0, 3)
                    }), ShaderVisibility.All)                                       
              });

            return desc;            
        }

        public static string kRayGenShader = "rayGen";
        public static string kMissShader = "miss";
        public static string kClosestHitShader = "chs";
        public static string kHitGroup = "HitGroup";

        public DxilLibrary CreateDxilLibrary()
        {
            // Compile the shader
            IDxcBlob pDxilLib = this.CompileLibrary("Data/Shaders.hlsl", new DxcShaderModel(6, 3));
            string[] entryPoints = new string[] { kRayGenShader, kMissShader, kClosestHitShader };
            return new DxilLibrary(pDxilLib, entryPoints);
        }
    }
}

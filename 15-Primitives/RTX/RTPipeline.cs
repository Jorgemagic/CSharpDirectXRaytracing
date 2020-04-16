﻿using RayTracingTutorial15.Structs;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;
using Vortice.Dxc;

namespace RayTracingTutorial15.RTX
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

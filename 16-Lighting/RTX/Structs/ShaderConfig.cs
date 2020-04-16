using Vortice.Direct3D12;

namespace RayTracingTutorial16.Structs
{
    public struct ShaderConfig
    {
        public RaytracingShaderConfig shaderConfig;
        public StateSubObject subObject;

        public ShaderConfig(int maxAttributeSizeInBytes, int maxPayloadSizeInBytes)
        {
            this.shaderConfig = new RaytracingShaderConfig(maxPayloadSizeInBytes, maxAttributeSizeInBytes);

            this.subObject = new StateSubObject(this.shaderConfig);
        }
    }
}

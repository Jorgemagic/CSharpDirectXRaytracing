﻿using Vortice.Direct3D12;

namespace RayTracingTutorial15.Structs
{
    public struct PipelineConfig
    {
        public RaytracingPipelineConfig config;
        public StateSubObject suboject;

        public PipelineConfig(int maxTraceRecursionDepth)
        {
            config = new RaytracingPipelineConfig(maxTraceRecursionDepth);

            suboject = new StateSubObject(config);
        }
    }
}

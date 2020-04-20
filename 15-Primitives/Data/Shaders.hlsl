#include "Data/Helpers.hlsl"

static const float3 cameraPosition = float3(0, 0, -2);
static const float4 backgroundColor = float4(0.4, 0.6, 0.2, 1.0);

RaytracingAccelerationStructure gRtScene : register(t0);
RWTexture2D<float4> gOutput : register(u0);

struct RayPayload
{
    float4 color;
};

[shader("raygeneration")]
void rayGen()
{
    uint3 launchIndex = DispatchRaysIndex();
    uint3 launchDim = DispatchRaysDimensions();

    float2 crd = float2(launchIndex.xy);
    float2 dims = float2(launchDim.xy);

    float2 d = ((crd / dims) * 2.f - 1.f);
    float aspectRatio = dims.x / dims.y;

    RayDesc ray;
    ray.Origin = cameraPosition;
    ray.Direction = normalize(float3(d.x * aspectRatio, -d.y, 1));

    ray.TMin = 0;
    ray.TMax = 100000;

    RayPayload payload;
    TraceRay(gRtScene,
        0 /*rayFlags*/,
        0xFF,
        0 /* ray index*/,
        0 /* Multiplies */,
        0 /* Miss index */,
        ray,
        payload);

    gOutput[launchIndex.xy] = linearToSrgb(payload.color);
}

[shader("miss")]
void miss(inout RayPayload payload)
{
    payload.color = backgroundColor;
}

[shader("closesthit")]
void chs(inout RayPayload payload, in BuiltInTriangleIntersectionAttributes attribs)
{
    float4 barycentrics = float4(1.0 - attribs.barycentrics.x - attribs.barycentrics.y, attribs.barycentrics.x, attribs.barycentrics.y,1);

    const float4 Red = float4(1, 0, 0, 1);
    const float4 Green = float4(0, 1, 0, 1);
    const float4 Blue = float4(0, 0, 1, 1);

    payload.color = Red * barycentrics.x + Green * barycentrics.y + Blue * barycentrics.z;
}
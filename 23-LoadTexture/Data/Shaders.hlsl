#include "Data/Helpers.hlsl"

RaytracingAccelerationStructure gRtScene : register(t0);
RWTexture2D<float4> gOutput : register(u0);

ByteAddressBuffer Indices : register(t1);
StructuredBuffer<VertexPositionNormalTangentTexture> Vertices : register(t2);

Texture2D DiffuseTexture : register(t3);
SamplerState DiffuseSampler : register(s0);

struct RayPayload
{
	float4 color;
	uint recursionDepth;
};

struct ShadowPayload
{
	bool hit;
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
	payload.recursionDepth = 0;
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
	float4 gradientStart = float4(0.4, 0.6, 0.2, 1.0);
	float4 gradientEnd = float4(0.5, 0.6, 1.0, 1.0);
	float3 unitDir = normalize(WorldRayDirection());
	float t = 0.5 * unitDir.y;
	payload.color = (1.0 - t) * gradientStart + t * gradientEnd;
	//payload.color = backgroundColor;
}

float3 HitAttribute(float3 vertexAttribute[3], BuiltInTriangleIntersectionAttributes attr)
{
	return vertexAttribute[0] +
		attr.barycentrics.x * (vertexAttribute[1] - vertexAttribute[0]) +
		attr.barycentrics.y * (vertexAttribute[2] - vertexAttribute[0]);
}

[shader("closesthit")]
void chs(inout RayPayload payload, in BuiltInTriangleIntersectionAttributes attribs)
{
	float3 hitPosition = HitWorldPosition();

	// Get the base index of the triangle's first 16 bit index.
	uint indexSizeInBytes = 2;
	uint indicesPerTriangle = 3;
	uint triangleIndexStride = indicesPerTriangle * indexSizeInBytes;
	uint baseIndex = PrimitiveIndex() * triangleIndexStride;

	// Load up 3 16 bit indices for the triangle.
	const uint3 indices = Load3x16BitIndices(Indices, baseIndex);

	// Retrieve corresponding vertex normals for the triangle vertices.
	float3 vertexNormals[3] = {
		Vertices[indices[0]].Normal,
		Vertices[indices[1]].Normal,
		Vertices[indices[2]].Normal
	};

	float3 hitNormal = (InstanceID() == 0) ? float3(0, 1, 0) : HitAttribute(vertexNormals, attribs);

	float3 vertexCoords[3] = {
		float3(Vertices[indices[0]].TexCoord, 0),
		float3(Vertices[indices[1]].TexCoord, 0),
		float3(Vertices[indices[2]].TexCoord, 0)
	};

	float3 hitUV = HitAttribute(vertexCoords, attribs);
	//float4 colorSampled = DiffuseTexture.Load(float3(hitUV.x*2048, hitUV.y*2048, 0));
	float4 colorSampled = DiffuseTexture.SampleLevel(DiffuseSampler, hitUV.xy, 0);

	float4 color;
	float4 diffuseColor = (InstanceID() == 0) ? groundAlbedo : colorSampled;
	if (payload.recursionDepth < MaxRecursionDepth)
	{
		// Shadow
		RayDesc shadowRay;
		shadowRay.Origin = hitPosition;
		shadowRay.Direction = normalize(lightPosition - shadowRay.Origin);
		shadowRay.TMin = 0.01;
		shadowRay.TMax = 100000;
		ShadowPayload shadowPayload;
		TraceRay(gRtScene,
			0  /*rayFlags*/,
			0xFF,
			1 /* ray index*/,
			0 /* Multiplies */,
			1 /* Miss index (shadow) */,
			shadowRay,
			shadowPayload);

		// Reflection    
		RayDesc reflectionRay;
		reflectionRay.Origin = hitPosition;
		reflectionRay.Direction = reflect(WorldRayDirection(), hitNormal);
		reflectionRay.TMin = 0.01;
		reflectionRay.TMax = 100000;
		RayPayload reflectionPayload;
		reflectionPayload.recursionDepth = payload.recursionDepth + 1;
		TraceRay(gRtScene,
			0  /*rayFlags*/,
			0xFF,
			0 /* ray index*/,
			0 /* Multiplies */,
			0 /* Miss index (raytrace) */,
			reflectionRay,
			reflectionPayload);
		float4 reflectionColor = reflectionPayload.color;

		float3 fresnelR = FresnelReflectanceSchlick(WorldRayDirection(), hitNormal, diffuseColor);
		float4 reflectedColor = reflectanceCoef * float4(fresnelR, 1) * reflectionColor;

		// Calculate final color.
		float4 phongColor = CalculatePhongLighting(diffuseColor, hitNormal, shadowPayload.hit, diffuseCoef, specularCoef, specularPower);
		color = phongColor +reflectedColor;
	}
	else
	{
		color = CalculatePhongLighting(diffuseColor, hitNormal, false, diffuseCoef, specularCoef, specularPower);
	}

	// Apply visibility falloff.
	float t = RayTCurrent();
	color = lerp(color, backgroundColor, 1.0 - exp(-0.000002 * t * t * t));

	payload.color = color;
}

[shader("miss")]
void shadowMiss(inout ShadowPayload payload)
{
	payload.hit = false;
}
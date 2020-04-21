#include "Data/Helpers.hlsl"

RaytracingAccelerationStructure gRtScene : register(t0);
RWTexture2D<float4> gOutput : register(u0);

ByteAddressBuffer Indices : register(t1);
StructuredBuffer<VertexPositionNormalTangentTexture> Vertices : register(t2);

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
	float2 xy = DispatchRaysIndex().xy + 0.5f; // center in the middle of the pixel.
	float2 screenPos = xy / DispatchRaysDimensions().xy * 2.0 - 1.0;

	// Invert Y for DirectX-style coordinates.
	screenPos.y = -screenPos.y;

	// Unproject the pixel coordinate into a ray.	
	float4 world = mul(float4(screenPos, 0, 1), projectionToWorld);
	world.xyz /= world.w;

	RayDesc ray;
	ray.Origin = cameraPosition.xyz;
	ray.Direction = normalize(world.xyz - ray.Origin);

	ray.TMin = 0;
	ray.TMax = 1000;

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

	gOutput[xy] = linearToSrgb(payload.color);
}

[shader("miss")]
void miss(inout RayPayload payload)
{
	payload.color = backgroundColor;
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

	float4 color;
	float4 diffuseColor2;
	uint instanceID = InstanceID();
	switch (instanceID)
	{
		case 0:
			diffuseColor2 = float4(1, 1, 1, 1);
			break;
		case 1:
			diffuseColor2 = float4(1, 0, 0, 1);
			break;
		case 2:
			diffuseColor2 = float4(0, 1, 0, 1);
			break;
		case 3:
			diffuseColor2 = float4(0, 0, 1, 1);
			break;
		case 4:
			diffuseColor2 = float4(0, 1, 1, 1);
			break;
		case 5:
			diffuseColor2 = float4(1, 1, 0, 1);
			break;		
	}
	if (payload.recursionDepth < MaxRecursionDepth)
	{
		// Shadow
		RayDesc shadowRay;
		shadowRay.Origin = hitPosition;
		shadowRay.Direction = normalize(lightPosition - shadowRay.Origin);
		shadowRay.TMin = 0.01;
		shadowRay.TMax = 1000;
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
		reflectionRay.TMax = 1000;
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
		float4 phongColor = CalculatePhongLighting(diffuseColor2, hitNormal, shadowPayload.hit, diffuseCoef, specularCoef, specularPower);
		color = phongColor + reflectedColor;
	}
	else
	{
		color = CalculatePhongLighting(diffuseColor2, hitNormal, false, 0.9, 0.7, 50);
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
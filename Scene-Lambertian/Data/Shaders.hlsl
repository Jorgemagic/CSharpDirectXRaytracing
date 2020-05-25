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
	if (payload.recursionDepth >= MaxRecursionDepth)
	{
		payload.color = float4(0, 0, 0, 1);
		return;
	}

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
	if (payload.recursionDepth < MaxRecursionDepth)
	{			
		color = CalculatePhongLighting(diffuseColor, hitNormal, false, diffuseCoef, specularCoef, specularPower);
	}

	payload.color = color;
}
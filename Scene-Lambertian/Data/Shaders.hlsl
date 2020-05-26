#include "Data/Helpers.hlsl"

RaytracingAccelerationStructure gRtScene : register(t0);
RWTexture2D<float4> gOutput : register(u0);

ByteAddressBuffer Indices : register(t1);
StructuredBuffer<VertexPositionNormalTangentTexture> Vertices : register(t2);

//RWBuffer<float> Random : register(u1);

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
	float4 gradientStart = float4(1.0, 1.0, 1.0, 1.0);
	float4 gradientEnd = float4(0.5, 0.7, 1.0, 1.0);

	float3 unitDir = normalize(WorldRayDirection());
	float t = 0.5 * (unitDir.y + 1.0);
	payload.color = (1.0 - t) * gradientStart + t * gradientEnd;  // blendedValue = (1 - t) * startValue + t * endValue
}

float3 HitAttribute(float3 vertexAttribute[3], BuiltInTriangleIntersectionAttributes attr)
{
	return vertexAttribute[0] +
		attr.barycentrics.x * (vertexAttribute[1] - vertexAttribute[0]) +
		attr.barycentrics.y * (vertexAttribute[2] - vertexAttribute[0]);
}

// Random utilities
uint wang_hash(uint seed)
{
	seed = (seed ^ 61) ^ (seed >> 16);
	seed *= 9;
	seed = seed ^ (seed >> 4);
	seed *= 0x27d4eb2d;
	seed = seed ^ (seed >> 15);
	return seed;
}

float randFloat()
{
	uint3 index = DispatchRaysIndex();
	return wang_hash(index.x * index.y) * (1.0 / 4294967296.0);
}

float RandomFloat(float min, float max)
{
	return randFloat() * (max - min) + min;
	/*uint2 pixelCoords = DispatchRaysIndex().xy;
	int index = pixelCoords.x * pixelCoords.y;
	return Random[index] * (max - min) + min;*/
}

static const float PI = 3.14159265f;
float3 RandomUnitVector()
{
	float a = RandomFloat(0, 2.0f * PI);
	float z = RandomFloat(-1, 1);
	float r = sqrt(1 - z * z);
	return float3(r * cos(a), r * sin(a), z);
}

float3 Random_in_hemisphere(float3 normal)
{
	float3 in_unit_sphere = RandomUnitVector();
	if (dot(in_unit_sphere, normal) > 0.0) // In the same hemisphere as the normal
		return in_unit_sphere;
	else
		return -in_unit_sphere;
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

	float3 hitNormal = HitAttribute(vertexNormals, attribs);

	float4 color;
	if (payload.recursionDepth < MaxRecursionDepth)
	{			
		RayDesc scatteredRay;
		scatteredRay.Origin = hitPosition;
		if (materialType == 0)
		{
			//scatteredRay.Direction = hitPosition + Random_in_hemisphere(hitNormal);
			scatteredRay.Direction = hitNormal + RandomUnitVector();
		}
		else
		{
			scatteredRay.Direction = reflect(normalize(WorldRayDirection()), hitNormal) + fuzz * RandomUnitVector();
		}
		scatteredRay.TMin = 0.01;
		scatteredRay.TMax = 100000;
		RayPayload scatteredPayload;
		scatteredPayload.recursionDepth = payload.recursionDepth + 1;
		TraceRay(gRtScene,
			0  /*rayFlags*/,
			0xFF,
			0 /* ray index*/,
			0 /* Multiplies */,
			0 /* Miss index (raytrace) */,
			scatteredRay,
			scatteredPayload);
		color = diffuseColor * scatteredPayload.color;
	}

	payload.color = color;
}
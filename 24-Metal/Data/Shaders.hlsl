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
	ray.TMax = 10000;

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
	float4 diffuseColor = (InstanceID() == 0) ? groundAlbedo : primitiveAlbedo;

	// Lambertian
	RayDesc scatteredRay;
	scatteredRay.Origin = hitPosition;

	uint instance = InstanceID();
	if (instance == 0 || instance == 1)
	{
		scatteredRay.Direction = hitNormal + RandomUnitVector();
	}
	else if (instance == 2)
	{
		scatteredRay.Direction = reflect(normalize(WorldRayDirection()), hitNormal) + 1.0 * RandomUnitVector();
	}
	else
	{
		scatteredRay.Direction = reflect(normalize(WorldRayDirection()), hitNormal) + 0.3 * RandomUnitVector();
	}
	scatteredRay.TMin = 0.01;
	scatteredRay.TMax = 100;
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

	if (instance == 0)
	{
		color = float4(0.8,0.8,0,1) * scatteredPayload.color;
	}
	else if (instance == 1)
	{
		color = float4(0.7,0.3,0.3,1) * scatteredPayload.color;
	}
	else if (instance == 2)
	{
		color = float4(0.8, 0.6, 0.2, 1) * scatteredPayload.color;
	}
	else if (instance == 3)
	{
		color = float4(0.8, 0.8, 0.8, 1) * scatteredPayload.color;		
	}	

	payload.color = color;
}

[shader("miss")]
void shadowMiss(inout ShadowPayload payload)
{
	payload.hit = false;
}
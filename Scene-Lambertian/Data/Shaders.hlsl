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
	uint2 index = DispatchRaysIndex().xy;
	uint2 dims = DispatchRaysDimensions().xy;
	float4 pixelColor = float4(0, 0, 0, 0);

	[unroll]
	for (int i = 0; i < 1; i++)
	{
		[unroll]
		for (int j = 0; j < 1; j++)
		{
			float2 xy = float2(index.xy) + float2(0.25f + 0.5f * i, 0.25f + 0.5f * j);

			float2 screenPos = xy / dims * 2.0 - 1.0;

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

			pixelColor += payload.color;
		}
	}

	gOutput[index] = linearToSrgb(float4(pixelColor.xyz / 1.0f, 1.0f));
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

	uint seed = initRand(DispatchRaysIndex().x * frameCount, DispatchRaysIndex().y * frameCount, 16);

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
			scatteredRay.Direction = hitNormal + CosineWeightedHemisphereSample(seed, hitNormal);
		}
		else
		{
			scatteredRay.Direction = reflect(normalize(WorldRayDirection()), hitNormal) + fuzz * RandomPointInUnitSphere(seed);
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
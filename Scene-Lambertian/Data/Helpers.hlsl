cbuffer SceneCB : register(b0)
{
	float4x4 projectionToWorld		: packoffset(c0);
	float4 backgroundColor			: packoffset(c4);
	float3 cameraPosition			: packoffset(c5);
	float MaxRecursionDepth : packoffset(c5.w);
	float3 lightPosition			: packoffset(c6);
	float4 lightAmbientColor		: packoffset(c7);
	float4 lightDiffuseColor		: packoffset(c8);
};

cbuffer PrimitiveCB : register(b1)
{
	float4 diffuseColor		: packoffset(c0);
	int materialType		: packoffset(c1.x);
	float fuzz				: packoffset(c1.y);	
}

struct VertexPositionNormalTangentTexture
{
	float3 Position;
	float3 Normal;
	float3 Tangent;
	float2 TexCoord;
};

float4 linearToSrgb(float4 c)
{
	// Based on http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html
	float4 sq1 = sqrt(c);
	float4 sq2 = sqrt(sq1);
	float4 sq3 = sqrt(sq2);
	float4 srgb = 0.662002687 * sq1 + 0.684122060 * sq2 - 0.323583601 * sq3 - 0.0225411470 * c;
	return srgb;
}

// Retrieve hit world position.
float3 HitWorldPosition()
{
	return WorldRayOrigin() + RayTCurrent() * WorldRayDirection();
}

// Load three 16 bit indices from a byte addressed buffer.
uint3 Load3x16BitIndices(ByteAddressBuffer Indices, uint offsetBytes)
{
	uint3 indices;

	// ByteAdressBuffer loads must be aligned at a 4 byte boundary.
	// Since we need to read three 16 bit indices: { 0, 1, 2 } 
	// aligned at a 4 byte boundary as: { 0 1 } { 2 0 } { 1 2 } { 0 1 } ...
	// we will load 8 bytes (~ 4 indices { a b | c d }) to handle two possible index triplet layouts,
	// based on first index's offsetBytes being aligned at the 4 byte boundary or not:
	//  Aligned:     { 0 1 | 2 - }
	//  Not aligned: { - 0 | 1 2 }
	const uint dwordAlignedOffset = offsetBytes & ~3;
	const uint2 four16BitIndices = Indices.Load2(dwordAlignedOffset);

	// Aligned: { 0 1 | 2 - } => retrieve first three 16bit indices
	if (dwordAlignedOffset == offsetBytes)
	{
		indices.x = four16BitIndices.x & 0xffff;
		indices.y = (four16BitIndices.x >> 16) & 0xffff;
		indices.z = four16BitIndices.y & 0xffff;
	}
	else // Not aligned: { - 0 | 1 2 } => retrieve last three 16bit indices
	{
		indices.x = (four16BitIndices.x >> 16) & 0xffff;
		indices.y = four16BitIndices.y & 0xffff;
		indices.z = (four16BitIndices.y >> 16) & 0xffff;
	}

	return indices;
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
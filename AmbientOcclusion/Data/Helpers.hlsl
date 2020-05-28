cbuffer SceneCB : register(b0)
{
	float4x4 projectionToWorld		: packoffset(c0);
	float4 backgroundColor			: packoffset(c4);
	float3 cameraPosition			: packoffset(c5);
	float MaxRecursionDepth			: packoffset(c5.w);
	float3 lightPosition			: packoffset(c6);
	int frameCount					: packoffset(c6.w);
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

// Random number generation
uint initRand(uint val0, uint val1, uint backoff = 16)
{
	uint v0 = val0, v1 = val1, s0 = 0;

	[unroll]
	for (uint n = 0; n < backoff; n++)
	{
		s0 += 0x9e3779b9;
		v0 += ((v1 << 4) + 0xa341316c) ^ (v1 + s0) ^ ((v1 >> 5) + 0xc8013ea4);
		v1 += ((v0 << 4) + 0xad90777d) ^ (v0 + s0) ^ ((v0 >> 5) + 0x7e95761e);
	}
	return v0;
}

float nextRand(inout uint s)
{
	s = (1664525u * s + 1013904223u);
	return float(s & 0x00FFFFFF) / float(0x01000000);
}

float3 GetPerpendicularVector(float3 u)
{
	float3 a = abs(u);
	uint xm = ((a.x - a.y) < 0 && (a.x - a.z) < 0) ? 1 : 0;
	uint ym = (a.y - a.z) < 0 ? (1 ^ xm) : 0;
	uint zm = 1 ^ (xm | ym);
	return cross(u, float3(xm, ym, zm));
}

float3 CosineWeightedHemisphereSample(inout uint seed, float3 normal)
{
	float2 random = float2(nextRand(seed), nextRand(seed));

	float3 bitangent = GetPerpendicularVector(normal);
	float3 tangent = cross(bitangent, normal);
	float r = sqrt(random.x);
	float phi = 2.0f * 3.14159265f * random.y;

	return tangent * (r * cos(phi).x) + bitangent * (r * sin(phi)) + normal.xyz * sqrt(1 - random.x);
}

inline float3 RandomPointInUnitSphere(inout uint seed)
{
	float3 p = float3(2.0f, 2.0f, 2.0f);

	while (length(p) > 1.0f)
	{
		p = float3(nextRand(seed) * 2.0f - 1.0f, nextRand(seed) * 2.0f - 1.0f, nextRand(seed) * 2.0f - 1.0f);
	}

	return p;
}

// Diffuse lighting calculation.
float CalculateDiffuseCoefficient(in float3 hitPosition, in float3 incidentLightRay, in float3 normal)
{
	float fNDotL = saturate(dot(-incidentLightRay, normal));
	return fNDotL;
}

// Phong lighting specular component
float4 CalculateSpecularCoefficient(in float3 hitPosition, in float3 incidentLightRay, in float3 normal, in float specularPower)
{
	float3 reflectedLightRay = normalize(reflect(incidentLightRay, normal));
	return pow(saturate(dot(reflectedLightRay, normalize(-WorldRayDirection()))), specularPower);
}


// Phong lighting model = ambient + diffuse + specular components.
float4 CalculatePhongLighting(in float4 albedo, in float3 normal, in bool isInShadow, in float diffuseCoef = 1.0, in float specularCoef = 1.0, in float specularPower = 50)
{
	float3 hitPosition = HitWorldPosition();
	float shadowFactor = isInShadow ? 0.35f : 1.0;
	float3 incidentLightRay = normalize(hitPosition - lightPosition);

	// Diffuse component.
	float Kd = CalculateDiffuseCoefficient(hitPosition, incidentLightRay, normal);
	float4 diffuseColor = shadowFactor * diffuseCoef * Kd * lightDiffuseColor * albedo;

	// Specular component.
	float4 specularColor = float4(0, 0, 0, 0);
	if (!isInShadow)
	{
		float4 lightSpecularColor = float4(1, 1, 1, 1);
		float4 Ks = CalculateSpecularCoefficient(hitPosition, incidentLightRay, normal, specularPower);
		specularColor = specularCoef * Ks * lightSpecularColor;
	}

	// Ambient component.
	// Fake AO: Darken faces with normal facing downwards/away from the sky a little bit.
	float4 ambientColor = lightAmbientColor;
	float4 ambientColorMin = lightAmbientColor - 0.15;
	float4 ambientColorMax = lightAmbientColor;
	float a = 1 - saturate(dot(normal, float3(0, -1, 0)));
	ambientColor = albedo * lerp(ambientColorMin, ambientColorMax, a);

	return ambientColor + diffuseColor + specularColor;
}
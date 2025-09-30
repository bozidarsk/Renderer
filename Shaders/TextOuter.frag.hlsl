#pragma stage fragment
#pragma cull none

#include <common.hlsl>

cbuffer ObjectUniforms : register(b1)
{
	float4 COLOR;
}

struct OuterFragment 
{
	float4 position : SV_POSITION;
	float2 uv;
	float3 normal;
	float3 worldPosition;
};

float4 main(OuterFragment input, bool frontFace : SV_IsFrontFace) 
{
	float3 viewDirection = normalize(CAMERA_POSITION - input.worldPosition);

	if (dot(input.normal, viewDirection) < 0)
		discard;

	if (!frontFace) 
	{
		if (input.uv.x * input.uv.x < input.uv.y)
			return COLOR;
	}
	else 
	{
		if (input.uv.x * input.uv.x > input.uv.y)
			return COLOR;
	}

	discard;
}

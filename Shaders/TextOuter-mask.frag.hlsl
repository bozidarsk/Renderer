#pragma stage fragment
#pragma cull none
#pragma blend disable

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

uint main(OuterFragment input, bool frontFace : SV_IsFrontFace) 
{
	float3 viewDirection = normalize(CAMERA_POSITION - input.worldPosition);

	if (dot(input.normal, viewDirection) < 0)
		discard;

	if (!frontFace) 
	{
		if (input.uv.x * input.uv.x < input.uv.y)
			return ID;
	}
	else 
	{
		if (input.uv.x * input.uv.x > input.uv.y)
			return ID;
	}

	discard;
}

#pragma stage fragment
#pragma cull none

#include <common.hlsl>

cbuffer Uniforms : UNIFORMS
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

struct PSOutput
{
	float4 color : SV_Target0;
	uint id : SV_Target1;
};

PSOutput main(OuterFragment input, bool frontFace : SV_IsFrontFace)
{
	PSOutput output;
	output.color = COLOR;
	output.id = ID;

	float3 viewDirection = normalize(CAMERA_POSITION - input.worldPosition);

	if (dot(input.normal, viewDirection) < 0)
		discard;

	if (!frontFace)
	{
		if (input.uv.x * input.uv.x < input.uv.y)
			return output;
	}
	else
	{
		if (input.uv.x * input.uv.x > input.uv.y)
			return output;
	}

	discard;
}

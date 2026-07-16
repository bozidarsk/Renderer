#pragma stage fragment
#pragma cull back
#pragma blend srcalpha oneminussrcalpha

#include <common.hlsl>

cbuffer ObjectUniforms : register(b1)
{
	float4 COLOR;
}

struct RectangleVertex
{
	float3 position;
};

struct RectangleFragment
{
	float4 position : SV_POSITION;
};

struct PSOutput
{
	float4 color : SV_Target0;
	uint id : SV_Target1;
};

PSOutput main(RectangleFragment input)
{
	PSOutput output;

	output.color = COLOR;
	output.id = ID;

	return output;
}

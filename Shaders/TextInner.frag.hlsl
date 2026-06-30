#pragma stage fragment
#pragma cull back

#include <common.hlsl>

cbuffer ObjectUniforms : register(b1)
{
	float4 COLOR;
}

struct InnerFragment
{
	float4 position : SV_POSITION;
};

struct PSOutput
{
	float4 color : SV_Target0;
	uint id : SV_Target1;
};

PSOutput main(InnerFragment input)
{
	PSOutput output;
	output.color = COLOR;
	output.id = ID;

	return output;
}

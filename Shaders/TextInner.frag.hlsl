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

float4 main(InnerFragment input) 
{
	return COLOR;
}

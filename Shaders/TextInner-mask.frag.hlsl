#pragma stage fragment
#pragma cull back
#pragma blend disable

#include <common.hlsl>

cbuffer ObjectUniforms : register(b1)
{
	float4 COLOR;
}

struct InnerFragment 
{
	float4 position : SV_POSITION;
};

uint main(InnerFragment input) 
{
	return ID;
}

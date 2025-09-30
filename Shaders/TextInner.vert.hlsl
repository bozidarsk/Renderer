#pragma stage vertex

#include <common.hlsl>

struct InnerVertex 
{
	float3 position;
};

struct InnerFragment 
{
	float4 position : SV_POSITION;
};

InnerFragment main(InnerVertex input) 
{
	InnerFragment output;

	output.position = PROJECTION * (VIEW * (MODEL * float4(input.position, 1)));

	return output;
}

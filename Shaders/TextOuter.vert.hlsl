#pragma stage vertex

#include <common.hlsl>

struct OuterVertex 
{
	float3 position;
	float2 uv;
	float3 normal;
};

struct OuterFragment 
{
	float4 position : SV_POSITION;
	float2 uv;
	float3 normal;
	float3 worldPosition;
};

OuterFragment main(OuterVertex input) 
{
	OuterFragment output;

	output.position = PROJECTION * (VIEW * (MODEL * float4(input.position, 1)));
	output.normal = normalize((MODEL * float4(input.normal, 0)).xyz);
	output.worldPosition = (MODEL * float4(input.position, 1)).xyz;
	output.uv = input.uv;

	return output;
}

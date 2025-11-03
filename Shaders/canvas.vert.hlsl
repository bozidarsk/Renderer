#pragma stage vertex

#include <common.hlsl>

struct CanvasVertex 
{
	float3 position;
	float2 uv;
};

struct CanvasFragment 
{
	float4 position : SV_POSITION;
	float2 uv;
};

CanvasFragment main(CanvasVertex input) 
{
	CanvasFragment output;

	output.position = float4(input.position.xy, 1, 1);
	output.uv = input.uv;

	return output;
}

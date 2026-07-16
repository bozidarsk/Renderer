#pragma stage vertex

#include <common.hlsl>

struct RectangleVertex
{
	float3 position;
};

struct RectangleFragment
{
	float4 position : SV_POSITION;
};

RectangleFragment main(RectangleVertex input)
{
	RectangleFragment output;

	output.position = PROJECTION * (VIEW * (MODEL * float4(input.position, 1)));

	return output;
}

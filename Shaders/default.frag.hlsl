#pragma stage fragment
#pragma cull back

#include <common.hlsl>

float4 main(Fragment input)
{
	return float4(input.worldPosition, 1);
}

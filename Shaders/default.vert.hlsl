#pragma stage vertex

#include <common.hlsl>

Fragment main(Vertex input)
{
	Fragment output;

	output.position = PROJECTION * (VIEW * (MODEL * float4(input.position, 1)));
	output.worldPosition = (MODEL * float4(input.position, 1)).xyz;
	output.normal = PROJECTION * (VIEW * (MODEL * float4(input.normal, 0)));
	output.uv = input.uv;
	output.color = float4(input.position, 1);

	return output;
}

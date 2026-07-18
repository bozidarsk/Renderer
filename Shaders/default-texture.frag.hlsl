#pragma stage fragment
#pragma cull back

#include <common.hlsl>

Texture2D texture0 : register(t2);
SamplerState texture0Sampler : register(s2);

float4 main(Fragment input)
{
	float2 uv = float2(1 - input.uv.x, input.uv.y);
	float border = 0.01;

	if (uv.x < border || uv.x > 1 - border || uv.y < border || uv.y > 1 - border)
		return float4(1);

	return texture0.Sample(texture0Sampler, uv);
}

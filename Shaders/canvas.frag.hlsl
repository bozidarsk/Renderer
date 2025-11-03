#pragma stage fragment
#pragma cull back
#pragma blend srcalpha oneminussrcalpha

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

[vk::binding(2)] Texture2D texture0 : register(t0);
[vk::binding(2)] SamplerState texture0Sampler : register(s0);

float4 main(CanvasFragment input) 
{
	float2 uv = float2(input.uv.x, 1 - input.uv.y);

	return texture0.Sample(texture0Sampler, uv);
}

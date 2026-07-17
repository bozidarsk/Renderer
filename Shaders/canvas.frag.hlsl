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

Texture2D texture0 : register(t2);
SamplerState texture0Sampler : register(s2);

float4 main(CanvasFragment input)
{
	float2 uv = float2(input.uv.x, input.uv.y);

	return texture0.Sample(texture0Sampler, uv);
}

#pragma stage fragment
#pragma cull back

#include <common.hlsl>

Texture2D texture0 : TEXTURE0;
SamplerState texture0Sampler : SAMPLER0;

float4 main(Fragment input)
{
	return texture0.Sample(texture0Sampler, input.uv);
}

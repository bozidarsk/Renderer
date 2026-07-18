// pipeline specific options:
// #pragma stage {Vulkan.ShaderStage} [entryPoint]
// #pragma cull {Vulkan.CullMode}
// #pragma frontface {Vulkan.FrontFace}
// #pragma blend {disabled|off}|{{Vulkan.BlendFactor} [Vulkan.BlendOp] {Vulkan.BlendFactor}}

// compiler specific options:
// #pragma language {glsl|hlsl}
// #pragma {Vulkan.ShaderCompiler.Limit} {value}
// #pragma environment {Vulkan.ShaderCompiler.TargetEnvironment} {Vulkan.ShaderCompiler.EnvironmentVersion}
// #pragma spirv {Vulkan.ShaderCompiler.SPIRVVersion}
// #pragma optimization {disabled|off}|{Vulkan.ShaderCompiler.OptimizationLevel}
// #pragma GenerateDebugInfo
// #pragma WarningsAsErrors
// #pragma SuppressWarnings
// #pragma AutoBindUniforms
// #pragma AutoCombinedImageSampler
// #pragma HLSLIOMapping
// #pragma HLSLOffsets
// #pragma PreserveBindings
// #pragma AutoMapLocations
// #pragma HLSLFunctionality1
// #pragma HLSL16BitTypes
// #pragma VulkanRulesRelaxed
// #pragma InvertY
// #pragma NanClamp

#ifndef COMMON_HLSL
#define COMMON_HLSL

struct Vertex
{
	float3 position;
	float3 normal;
	float2 uv;
	float4 color;
};

struct Fragment
{
	float4 position : SV_POSITION;
	float3 worldPosition;
	float3 normal;
	float2 uv;
	float4 color;
};

cbuffer GlobalUniforms : register(b0)
{
	float4x4 VIEW;
	float4x4 PROJECTION;
	float3 CAMERA_POSITION;
}

[[vk::push_constant]]
cbuffer PushConstants
{
	float4x4 MODEL;
	uint ID;
}

#endif

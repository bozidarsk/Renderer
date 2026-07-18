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
// #pragma GenerateDebugInfo [true|false]
// #pragma WarningsAsErrors [true|false]
// #pragma SuppressWarnings [true|false]
// #pragma AutoBindUniforms [true|false]
// #pragma AutoCombinedImageSampler [true|false]
// #pragma HLSLIOMapping [true|false]
// #pragma HLSLOffsets [true|false]
// #pragma PreserveBindings [true|false]
// #pragma AutoMapLocations [true|false]
// #pragma HLSLFunctionality1 [true|false]
// #pragma HLSL16BitTypes [true|false]
// #pragma VulkanRulesRelaxed [true|false]
// #pragma InvertY [true|false]
// #pragma NanClamp [true|false]

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

cbuffer GlobalUniforms : GLOBAL_UNIFORMS
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

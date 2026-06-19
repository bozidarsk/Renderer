using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Vulkan;
using Vulkan.ShaderCompiler;

namespace Renderer;

public sealed class ShaderProgram : IDisposable
{
	public Shader[] Shaders { get; }
	public ShaderModule[] Modules { get; }
	public PipelineShaderStageCreateInfo[] Stages { get; }

	public CullMode? CullMode => this.Shaders.Select(x => x.CullMode).FirstOrDefault(x => x != null);
	public FrontFace? FrontFace => this.Shaders.Select(x => x.FrontFace).FirstOrDefault(x => x != null);
	public BlendFactor? SourceBlendFactor => this.Shaders.Select(x => x.SourceBlendFactor).FirstOrDefault(x => x != null);
	public BlendFactor? DestinationBlendFactor => this.Shaders.Select(x => x.DestinationBlendFactor).FirstOrDefault(x => x != null);
	public BlendOp? BlendOp => this.Shaders.Select(x => x.BlendOp).FirstOrDefault(x => x != null);
	public bool? DisableBlending => this.Shaders.Select(x => x.DisableBlending).FirstOrDefault(x => x != null);

	public static ShaderProgram FromFiles(Renderer renderer, params string[] filenames) => renderer.CreateShaderProgram(filenames);

	public void Dispose()
	{
		for (int i = 0; i < Shaders.Length; i++)
		{
			Stages[i].Dispose();
			Modules[i].Dispose();
		}
	}

	internal ShaderProgram(Shader[] shaders, ShaderModule[] modules, PipelineShaderStageCreateInfo[] stages) =>
		(this.Shaders, this.Modules, this.Stages) = (shaders, modules, stages)
	;
}

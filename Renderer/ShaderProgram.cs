using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Vulkan;
using Vulkan.ShaderCompiler;

namespace Renderer;

public class ShaderProgram
{
	public Shader[] Shaders { get; }

	public CullMode? CullMode => this.Shaders.Select(x => x.CullMode).FirstOrDefault(x => x != null);
	public FrontFace? FrontFace => this.Shaders.Select(x => x.FrontFace).FirstOrDefault(x => x != null);
	public BlendFactor? SourceBlendFactor => this.Shaders.Select(x => x.SourceBlendFactor).FirstOrDefault(x => x != null);
	public BlendFactor? DestinationBlendFactor => this.Shaders.Select(x => x.DestinationBlendFactor).FirstOrDefault(x => x != null);
	public BlendOp? BlendOp => this.Shaders.Select(x => x.BlendOp).FirstOrDefault(x => x != null);
	public bool? DisableBlending => this.Shaders.Select(x => x.DisableBlending).FirstOrDefault(x => x != null);

	private static readonly List<Shader> compiledShaders = new();
	private static readonly Compiler shaderCompiler = new();
	private static readonly CompilerOptions shaderCompilerOptions = new()
	{
		Environment = (TargetEnvironment.Vulkan, EnvironmentVersion.Vulkan13),
		SPIRVVersion = SPIRVVersion.Version14,
		IncludeDirectories = ["Renderer/Shaders"],
		InvertY = true,
		MacroDefinitions = new[]
		{
			("GLOBAL_UNIFORMS", $"register(b{Renderer.GLOBAL_UNIFORMS_BINDING})"),
			("UNIFORMS", $"register(b{Renderer.OBJECT_UNIFORMS_BINDING})"),
		}.Concat(
			Enumerable.Range(0, Renderer.MAX_TEXTURES).Select<int, (string, string)[]>(x =>
				[
					($"TEXTURE{x}", $"register(t{Renderer.TEXTURES_BINDING + x})"),
					($"SAMPLER{x}", $"register(s{Renderer.TEXTURES_BINDING + x})")
				]
			).SelectMany(x => x)
		).ToArray()
	};

	public ShaderProgram(params string[] filenames)
	{
		if (filenames == null)
			throw new ArgumentNullException();

		this.Shaders = new Shader[filenames.Length];

		for (int i = 0; i < filenames.Length; i++)
		{
			if (!File.Exists(filenames[i]))
				throw new FileNotFoundException($"Shader file '{filenames[i]}' does not exist.");

			string filename = Path.GetFullPath(filenames[i]);

			var query = compiledShaders.Where(x => x.File == filename);

			if (!query.Any())
			{
				Shader shader = shaderCompiler.Compile(filename, shaderCompilerOptions);

				compiledShaders.Add(shader);
				this.Shaders[i] = shader;
			}
			else
			{
				this.Shaders[i] = query.First();
			}
		}
	}
}

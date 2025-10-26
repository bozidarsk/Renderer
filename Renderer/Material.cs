using System;
using System.Linq;
using System.Collections.Generic;

using Vulkan;

namespace Renderer;

public sealed class Material 
{
	public readonly ShaderInfo[] Shaders;
	private readonly Dictionary<string, object> uniforms = new();

	public IReadOnlyDictionary<string, object> Uniforms => uniforms;

	public object this[string name] 
	{
		set 
		{
			if (name == null || value == null)
				throw new ArgumentNullException();

			uniforms[name] = (value is IInfoProvider infoProvider) ? infoProvider.Info : value;
		}
	}

	public static Material FromShaders(string? vertex, string? fragment) => 
		new(vertex ?? "Renderer/Vulkan/Shaders/default.vert.hlsl", fragment ?? "Renderer/Vulkan/Shaders/default.frag.hlsl")
	;

	private Material(params string[] shaderPaths) 
	{
		if (shaderPaths == null)
			throw new ArgumentNullException();

		this.Shaders = shaderPaths.Select(x => new ShaderInfo(x)).ToArray();
	}
}

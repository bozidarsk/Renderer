using System;
using System.Collections.Generic;

using Vulkan;

namespace Renderer;

public sealed class Material
{
	private readonly Dictionary<string, object> uniforms = new();

	public ShaderProgram ShaderProgram { get; }
	public IReadOnlyDictionary<string, object> Uniforms => uniforms;

	public object this[string name]
	{
		set
		{
			if (name == null || value == null)
				throw new ArgumentNullException();

			Console.WriteLine(value.GetType());
			uniforms[name] = value;
		}
	}

	public Material(ShaderProgram shaderProgram)
	{
		if (shaderProgram == null)
			throw new ArgumentNullException();

		this.ShaderProgram = shaderProgram;
	}
}

using System;
using System.Collections.Generic;

namespace Renderer;

public sealed class Material
{
	public ShaderProgram ShaderProgram { get; }
	public IEnumerable<object?> Uniforms => uniforms.Values;

	private readonly Dictionary<string, object?> uniforms = new();

	public object this[string name]
	{
		set
		{
			if (name == null || value == null)
				throw new ArgumentNullException();

			uniforms[name] = uniforms.ContainsKey(name) ? value : throw new ArgumentException($"Material does not have a property named '{name}'.");
		}
	}

	public Material(string[] shaders, UniformDescription[]? uniforms)
	{
		if (shaders == null)
			throw new ArgumentNullException();

		this.ShaderProgram = new(shaders);

		if (uniforms != null)
		{
			foreach (var x in uniforms)
			{
				if (this.uniforms.ContainsKey(x.Name))
					throw new InvalidOperationException($"Material already contains a property with the same name '{x.Name}'.");

				if (x.DefaultValue != null && x.DefaultValue.GetType() != x.Type)
					throw new ArgumentException("The type of the default value must be the same as the type of the uniform.");

				if (!x.Type.IsValueType)
					this.uniforms[x.Name] = null;
				else if (x.DefaultValue != null)
					this.uniforms[x.Name] = x.DefaultValue;
				else
					this.uniforms[x.Name] = Activator.CreateInstance(x.Type);
			}
		}
	}

	public record UniformDescription(string Name, Type Type, object? DefaultValue = null);
}

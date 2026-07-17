using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;

using Json.Schema;

namespace Renderer;

public sealed class Material
{
	public ShaderProgram ShaderProgram { get; }
	public IEnumerable<object?> Uniforms => uniforms.Values;

	private readonly Dictionary<string, object?> uniforms = new();

	private static readonly JsonSchema schema = JsonSchema.FromFile(Path.GetFullPath("Renderer/Materials/schema.json"));

	public object this[string name]
	{
		set
		{
			if (name == null || value == null)
				throw new ArgumentNullException();

			uniforms[name] = uniforms.ContainsKey(name) ? value : throw new ArgumentException($"Material does not have a property named '{name}'.");
		}
	}

	public Material(string filename)
	{
		if (filename == null)
			throw new ArgumentNullException();

		if (!File.Exists(filename))
			throw new FileNotFoundException();

		var json = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(filename));

		var validationResult = schema.Evaluate(json);
		if (!validationResult.IsValid)
		{
			if (validationResult.Errors != null)
			{
				foreach ((var key, var value) in validationResult.Errors)
					Console.WriteLine($"[{key}, {value}]");
			}

			throw new JsonException($"Validation failed for '{filename}'.");
		}

		this.ShaderProgram = new(json.GetProperty("shaders").EnumerateArray().Select(x => x.GetString()!).ToArray());

		if (json.TryGetProperty("uniforms", out var jsonUniforms))
		{
			foreach (var jsonUniform in jsonUniforms.EnumerateArray())
			{
				var name = jsonUniform.GetProperty("name").GetString()!;
				var type = jsonUniform.GetProperty("type").GetString() switch
				{
					"int" => typeof(int),
					"uint" => typeof(uint),
					"float" => typeof(float),
					"Color" => typeof(Color),
					"Vector2" => typeof(Vector2),
					"Vector3" => typeof(Vector3),
					"Vector4" => typeof(Vector4),
					"Vector2Int" => typeof(Vector2Int),
					"Vector3Int" => typeof(Vector3Int),
					"Vector4Int" => typeof(Vector4Int),
					"Matrix4x4" => typeof(Matrix4x4),
					"Texture" => typeof(Texture),
					_ => throw new UnreachableException()
				};

				if (uniforms.ContainsKey(name))
					throw new InvalidOperationException($"Material already contains a property with the same name '{name}'.");

				if (!type.IsValueType)
					uniforms[name] = null;
				else if (jsonUniform.TryGetProperty("default", out var jsonDefaultValue))
					uniforms[name] = jsonDefaultValue.Deserialize(type);
				else
					uniforms[name] = Activator.CreateInstance(type);
			}
		}
	}
}

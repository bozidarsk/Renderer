using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Vulkan;

namespace Renderer;

public sealed class OBJ 
{
	private List<Vector3> vertices = new();
	public IReadOnlyList<Vector3> Vertices => vertices;

	private List<Vector2> textures = new();
	public IReadOnlyList<Vector2> Textures => textures;

	private List<Vector3> normals = new();
	public IReadOnlyList<Vector3> Normals => normals;

	private List<uint> indices = new();
	public IReadOnlyList<uint> Indices => indices;

	public static OBJ FromFile(string filename) => 
		(filename != null && File.Exists(filename)) ? new OBJ(filename) : throw new ArgumentException($"Invalid filename '{filename}'.")
	;

	private void RemoveDuplicates() 
	{
		var map = new Dictionary<VertexData, uint>();

		var vertices = new List<Vector3>();
		var textures = new List<Vector2>();
		var normals = new List<Vector3>();
		var indices = new List<uint>();

		for (int i = 0; i < this.indices.Count; i++) 
		{
			var vertex = new VertexData() 
			{
				v = (this.vertices.Count != 0) ? this.vertices[i] : default,
				t = (this.textures.Count != 0) ? this.textures[i] : default,
				n = (this.normals.Count != 0) ? this.normals[i] : default
			};

			var index = (uint)i;

			if (!map.TryGetValue(vertex, out index)) 
			{
				map[vertex] = (uint)vertices.Count;
				vertices.Add(vertex.v);
				textures.Add(vertex.t);
				normals.Add(vertex.n);
			}

			indices.Add(index);
		}

		this.vertices = vertices;
		this.textures = textures;
		this.normals = normals;
		this.indices = indices;
	}

	private bool IsValid() 
	{
		if (vertices.Count != normals.Count && normals.Count != 0)
			return false;

		if (vertices.Count != textures.Count && textures.Count != 0)
			return false;

		foreach (var x in indices)
			if (x >= (uint)vertices.Count)
				return false;

		return indices.Count % 3 == 0;
	}

	private OBJ(string filename) 
	{
		var lines = File.ReadAllLines(filename);

		var vertices = new List<Vector3>();
		var textures = new List<Vector2>();
		var normals = new List<Vector3>();

		foreach (var line in lines) 
		{
			var tokens = line.Split(' ');

			switch (tokens[0]) 
			{
				case "v":
					vertices.Add(new(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3])));
					continue;
				case "vt":
					textures.Add(new(float.Parse(tokens[1]), float.Parse(tokens[2])));
					continue;
				case "vn":
					normals.Add(new(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3])));
					continue;
				case "f":
					if (tokens.Length != 1 + 3)
						throw new NotImplementedException("Faces must be triangulated.");

					foreach (var face in tokens.Skip(1)) 
					{
						getFaceIndices(face, out int? vi, out int? ti, out int? ni);

						if (vi is int v)
							this.vertices.Add(vertices[(v < 0) ? vertices.Count + v : v - 1]);

						if (ti is int t)
							this.textures.Add(textures[(t < 0) ? textures.Count + t : t - 1]);

						if (ni is int n)
							this.normals.Add(normals[(n < 0) ? normals.Count + n : n - 1]);

						indices.Add((uint)indices.Count);
					}

					continue;
				case "":
				case "#":
				case "o":
				case "s":
				case "g":
				case "mtllib":
				case "usemtl":
					continue;
				default:
					throw new FormatException($"Failed to parse line '{line}'.");
			}
		}

		// RemoveDuplicates();

		if (!IsValid())
			throw new FormatException("Failed to parse obj file.");

		static void getFaceIndices(string face, out int? vi, out int? ti, out int? ni) 
		{
			var tokens = face.Split('/');

			(vi, ni, ti) = (null, null, null);

			switch (tokens.Length) 
			{
				case 1:
					vi = int.Parse(tokens[0]);
					break;
				case 2:
					vi = int.Parse(tokens[0]);
					ti = int.Parse(tokens[1]);
					break;
				case 3:
					vi = int.Parse(tokens[0]);
					ti = !string.IsNullOrEmpty(tokens[1]) ? int.Parse(tokens[1]) : null;
					ni = int.Parse(tokens[2]);
					break;
				default:
					throw new FormatException("Face indices count must be 1, 2 or 3.");
			}
		}
	}

	private struct VertexData 
	{
		public Vector3 v;
		public Vector2 t;
		public Vector3 n;
	}
}

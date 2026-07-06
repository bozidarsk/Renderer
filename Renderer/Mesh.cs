using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Diagnostics;

namespace Renderer;

public class Mesh
{
	public Array Vertices { get; }
	public Type VertexType => this.Vertices.GetType().GetElementType()!;
	public int VertexCount => this.Vertices.Length;

	public Array Indices { get; }
	public Type IndexType => this.Indices.GetType().GetElementType()!;
	public int IndexCount => this.Indices.Length;

	public static readonly Mesh<DefaultVertex, byte> Empty = new(
		[new DefaultVertex()],
		[0, 0, 0]
	);

	public Mesh(string filename) : this(filename, default, default) { }

	internal protected Mesh(string filename, Type? vertexType, Type? indexType)
	{
		if (filename == null)
			throw new ArgumentNullException();

		vertexType ??= typeof(DefaultVertex);
		indexType ??= typeof(uint);

		var extension = Path.GetExtension(filename).ToLower();
		switch (extension)
		{
			case ".obj":
				var obj = OBJ.FromFile(filename);
				this.Vertices = Array.CreateInstance(vertexType, obj.Vertices.Count);
				for (int i = 0; i < this.Vertices.Length; i++)
				{
					object v = this.Vertices.GetValue(i)!;

					((IVertex)v).Position = (obj.Vertices.Count != 0) ? obj.Vertices[i] : default;
					((IVertex)v).Normal = (obj.Normals.Count != 0) ? obj.Normals[i] : default;
					((IVertex)v).UV = (obj.Textures.Count != 0) ? obj.Textures[i] : default;

					this.Vertices.SetValue(v, i);
				}
				this.Indices = indexType switch
				{
					Type t when t == typeof(byte) => obj.Indices.Select(x => checked((byte)x)).ToArray(),
					Type t when t == typeof(ushort) => obj.Indices.Select(x => checked((ushort)x)).ToArray(),
					Type t when t == typeof(uint) => obj.Indices.Select(x => checked((uint)x)).ToArray(),
					_ => throw new UnreachableException()
				};
				break;
			default:
				throw new InvalidOperationException($"Failed to parse mesh of type '{extension}'.");
		}
	}

	internal protected Mesh(Array vertices, Array indices)
	{
		this.Vertices = vertices ?? throw new ArgumentNullException();
		this.Indices = indices ?? throw new ArgumentNullException();
	}
}

public class Mesh<TVertex> : Mesh
	where TVertex : struct, IVertex
{
	public Mesh(string filename) : base(filename, vertexType: typeof(TVertex), indexType: default) { }

	public Mesh(TVertex[] vertices, byte[] indices) : base(vertices, indices) { }
	public Mesh(TVertex[] vertices, ushort[] indices) : base(vertices, indices) { }
	public Mesh(TVertex[] vertices, uint[] indices) : base(vertices, indices) { }
}

public class Mesh<TVertex, TIndex> : Mesh
	where TVertex : struct, IVertex
	where TIndex : unmanaged, IBinaryInteger<TIndex>
{
	public Mesh(string filename) : base(
		filename,
		vertexType: typeof(TVertex),
		indexType: (typeof(TIndex) == typeof(byte) || typeof(TIndex) == typeof(ushort) || typeof(TIndex) == typeof(uint))
			? typeof(TIndex)
			: throw new ArgumentException("Index type must be byte, ushort or uint.")
	)
	{
	}

	public Mesh(TVertex[] vertices, byte[] indices) : base(vertices, indices) { }
	public Mesh(TVertex[] vertices, ushort[] indices) : base(vertices, indices) { }
	public Mesh(TVertex[] vertices, uint[] indices) : base(vertices, indices) { }
}

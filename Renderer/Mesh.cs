using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Diagnostics;

using Vulkan;

namespace Renderer;

public class Mesh : Asset
{
	internal Vulkan.Buffer VertexBuffer { private set; get; }
	internal DeviceMemory VertexBufferMemory { private set; get; }
	internal Vulkan.Buffer IndexBuffer { private set; get; }
	internal DeviceMemory IndexBufferMemory { private set; get; }

	internal int VertexCount { private set; get; }
	internal Type VertexType { private set; get; }

	internal int IndexCount { private set; get; }
	internal IndexType IndexType { private set; get; }

	public static readonly Mesh<DefaultVertex, byte> Empty = new(
		[new DefaultVertex()],
		[0, 0, 0]
	);

	private void Initialize(Array vertices, Array indices)
	{
		renderer.CreateStagingBuffer(vertices, BufferUsage.VertexBuffer, out var vertexBuffer, out var vertexBufferMemory);
		renderer.CreateStagingBuffer(indices, BufferUsage.IndexBuffer, out var indexBuffer, out var indexBufferMemory);

		this.VertexBuffer = vertexBuffer;
		this.VertexBufferMemory = vertexBufferMemory;
		this.IndexBuffer = indexBuffer;
		this.IndexBufferMemory = indexBufferMemory;

		this.VertexCount = vertices.Length;
		this.IndexCount = indices.Length;

		this.VertexType = vertices.GetType().GetElementType()!;
		this.IndexType = indices.GetType().GetElementType()! switch
		{
			Type t when t == typeof(byte) => IndexType.UInt8,
			Type t when t == typeof(ushort) => IndexType.UInt16,
			Type t when t == typeof(uint) => IndexType.UInt32,
			Type t => throw new ArgumentOutOfRangeException(nameof(IndexType), $"Cannot map mesh index type '{t.FullName}' to a vulkan index type.")
		};
	}

	protected override void Free()
	{
		renderer.ToBeDisposed(VertexBuffer);
		renderer.ToBeDisposed(VertexBufferMemory);
		renderer.ToBeDisposed(IndexBuffer);
		renderer.ToBeDisposed(IndexBufferMemory);
	}

#pragma warning disable CS8618

	public Mesh(string filename) : this(filename, default, default) { }

	internal protected Mesh(string filename, Type? vertexType, Type? indexType)
	{
		if (filename == null)
			throw new ArgumentNullException();

		vertexType ??= typeof(DefaultVertex);
		indexType ??= typeof(uint);

		Array vertices, indices;

		var extension = Path.GetExtension(filename).ToLower();
		switch (extension)
		{
			case ".obj":
				var obj = OBJ.FromFile(filename);
				vertices = Array.CreateInstance(vertexType, obj.Vertices.Count);
				for (int i = 0; i < vertices.Length; i++)
				{
					object v = vertices.GetValue(i)!;

					((IVertex)v).Position = (obj.Vertices.Count != 0) ? obj.Vertices[i] : default;
					((IVertex)v).Normal = (obj.Normals.Count != 0) ? obj.Normals[i] : default;
					((IVertex)v).UV = (obj.Textures.Count != 0) ? obj.Textures[i] : default;

					vertices.SetValue(v, i);
				}
				indices = indexType switch
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

		Initialize(vertices, indices);
	}

	internal protected Mesh(Array vertices, Array indices) =>
		Initialize(vertices ?? throw new ArgumentNullException(), indices ?? throw new ArgumentNullException())
	;

#pragma warning restore
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

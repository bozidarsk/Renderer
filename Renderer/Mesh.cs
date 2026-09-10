using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Diagnostics;
using System.Threading.Tasks;

using Vulkan;

namespace Renderer;

public class Mesh : Asset
{
	public Buffer VertexBuffer { get; }
	public Buffer IndexBuffer { get; }

	internal int VertexCount { get; }
	internal Type VertexType { get; }

	internal int IndexCount { get; }
	internal IndexType IndexType { get; }

	public static readonly Mesh Empty = Mesh.CreateAsync<DefaultVertex>([new DefaultVertex()], [0, 0, 0]).Result;

	protected override void Free()
	{
		renderer.ToBeDisposed(VertexBuffer);
		renderer.ToBeDisposed(IndexBuffer);
	}

	private static async Task<Mesh> FromArraysAsync<TVertex, TIndex>(TVertex[] vertices, TIndex[] indices)
		where TVertex : struct, IVertex
		where TIndex : unmanaged, IBinaryInteger<TIndex>
	{
		var vertexCount = vertices.Length;
		var indexCount = indices.Length;

		var vertexType = typeof(TVertex);
		var indexType = typeof(TIndex);

		if (vertexCount <= 0 || indexCount <= 0)
			throw new ArgumentOutOfRangeException();

		if (indexCount % 3 != 0)
			throw new ArgumentOutOfRangeException(nameof(indexCount), "Index count must be multiple of 3.");

		if (!vertexType.IsValueType || !vertexType.IsAssignableTo(typeof(IVertex)))
			throw new ArgumentException($"Vertex type must be a struct and implement '{nameof(IVertex)}'.");

		if (indexType != typeof(byte) && indexType != typeof(ushort) && indexType != typeof(uint))
			throw new ArgumentException("Index type must be byte, ushort or uint.");

		var vertexBuffer = await Buffer.CreateAsync(vertices, BufferUsage.VertexBuffer);
		var indexBuffer = await Buffer.CreateAsync(indices, BufferUsage.IndexBuffer);

		return new(vertexBuffer, vertexCount, vertexType, indexBuffer, indexCount, indexType);
	}

	public static async Task<Mesh> CreateAsync<TVertex>(TVertex[] vertices, byte[] indices) where TVertex : struct, IVertex => await FromArraysAsync(vertices, indices);
	public static async Task<Mesh> CreateAsync<TVertex>(TVertex[] vertices, ushort[] indices) where TVertex : struct, IVertex => await FromArraysAsync(vertices, indices);
	public static async Task<Mesh> CreateAsync<TVertex>(TVertex[] vertices, uint[] indices) where TVertex : struct, IVertex => await FromArraysAsync(vertices, indices);

	public static async Task<Mesh> CreateAsync(string filename) => await CreateAsync<DefaultVertex, uint>(filename);
	public static async Task<Mesh> CreateAsync<TVertex>(string filename) where TVertex : struct, IVertex => await CreateAsync<TVertex, uint>(filename);
	public static async Task<Mesh> CreateAsync<TVertex, TIndex>(string filename)
		where TVertex : struct, IVertex
		where TIndex : unmanaged, IBinaryInteger<TIndex>, IConvertible
	{
		if (filename == null)
			throw new ArgumentNullException();

		if (typeof(TIndex) != typeof(byte) && typeof(TIndex) != typeof(ushort) && typeof(TIndex) != typeof(uint))
			throw new ArgumentException("Index type must be byte, ushort or uint.");

		TVertex[] vertices;
		TIndex[] indices;

		var extension = Path.GetExtension(filename).ToLower();

		switch (extension)
		{
			case ".obj":
				var obj = OBJ.FromFile(filename);
				vertices = new TVertex[obj.Vertices.Count];
				for (int i = 0; i < vertices.Length; i++)
				{
					vertices[i].Position = (obj.Vertices.Count != 0) ? obj.Vertices[i] : default;
					vertices[i].Normal = (obj.Normals.Count != 0) ? obj.Normals[i] : default;
					vertices[i].UV = (obj.Textures.Count != 0) ? obj.Textures[i] : default;
				}
				indices = obj.Indices.Select(x => (TIndex)Convert.ChangeType(x, typeof(TIndex))).ToArray();
				break;
			default:
				throw new InvalidOperationException($"Failed to parse mesh of type '{extension}'.");
		}

		return await FromArraysAsync(vertices, indices);
	}

	private Mesh(Buffer vertexBuffer, int vertexCount, Type vertexType, Buffer indexBuffer, int indexCount, Type indexType)
	{
		this.VertexBuffer = vertexBuffer;
		this.IndexBuffer = indexBuffer;

		this.VertexCount = vertexCount;
		this.IndexCount = indexCount;

		this.VertexType = vertexType;
		this.IndexType = indexType switch
		{
			Type t when t == typeof(byte) => IndexType.UInt8,
			Type t when t == typeof(ushort) => IndexType.UInt16,
			Type t when t == typeof(uint) => IndexType.UInt32,
			_ => throw new UnreachableException()
		};
	}
}

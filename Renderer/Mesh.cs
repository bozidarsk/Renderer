using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using Vulkan;

namespace Renderer;

public abstract class Mesh : IDisposable
{
	internal readonly Vulkan.Buffer VertexBuffer;
	internal readonly DeviceMemory VertexBufferMemory;
	internal readonly Type VertexType;
	internal readonly Array VertexData;
	internal int VertexCount => this.VertexData.Length;

	internal readonly Vulkan.Buffer IndexBuffer;
	internal readonly DeviceMemory IndexBufferMemory;
	internal readonly IndexType IndexType;
	internal readonly Array IndexData;
	internal int IndexCount => this.IndexData.Length;

	public void Dispose() 
	{
		VertexBuffer.Dispose();
		VertexBufferMemory.Dispose();
		IndexBuffer.Dispose();
		IndexBufferMemory.Dispose();
	}

	#pragma warning disable CS8618
	private Mesh(Type vertexType, Type? indexType) 
	#pragma warning restore
	{
		this.VertexType = vertexType;

		if (!vertexType.IsValueType || !vertexType.IsAssignableTo(typeof(IVertex)))
			throw new ArgumentException($"VertexType must be a struct and implement IVertex, not '{vertexType}'.");

		if (indexType != null && indexType != typeof(byte) && indexType != typeof(ushort) && indexType != typeof(uint))
			throw new ArgumentException($"IndexType must be byte, ushort or uint, not '{indexType}'.");

		if (indexType != null) 
		{
			if (indexType == typeof(byte))
				this.IndexType = IndexType.UInt8;
			if (indexType == typeof(ushort))
				this.IndexType = IndexType.UInt16;
			if (indexType == typeof(uint))
				this.IndexType = IndexType.UInt32;
		}
	}

	protected Mesh(Vulkan.Program vk, Type vertexType, Type? indexType, Array vertexData, Array indexData) : this(vertexType, indexType)
	{
		if (vk == null || vertexData == null || indexData == null)
			throw new ArgumentNullException();

		if (vertexData.Length == 0)
			throw new ArgumentOutOfRangeException("Vertex array must have at least one vertex.");

		Type vertexDataType = vertexData.GetValue(0)!.GetType();
		if (vertexDataType != vertexType)
			throw new ArgumentException($"Vertex array elements must be '{vertexType}', not '{vertexDataType}'.");

		if (indexData.Length == 0 || indexData.Length % 3 == 0)
			throw new ArgumentOutOfRangeException("Indices count must be multiple of 3.");

		Type indexDataType = indexData.GetValue(0)!.GetType();
		if (indexDataType != typeof(byte) && indexDataType != typeof(ushort) && indexDataType != typeof(uint))
			throw new ArgumentException($"Index array elements must be byte, ushort or uint, not '{indexDataType}'.");

		uint maxIndex = 0;
		for (int i = 0; i < indexData.Length; i++)
			maxIndex = Math.Max(maxIndex, ((IConvertible)indexData.GetValue(i)!).ToUInt32(null));

		if (maxIndex >= vertexData.Length)
			throw new ArgumentOutOfRangeException("Index array references a vertex out of bounds of the vertex array.");

		if (indexType == null) 
		{
			this.IndexType = maxIndex switch 
			{
				<= byte.MaxValue => IndexType.UInt8,
				> byte.MaxValue and <= ushort.MaxValue => IndexType.UInt16,
				> ushort.MaxValue and <= uint.MaxValue => IndexType.UInt32
			};
		}

		this.VertexData = Array.CreateInstance(this.VertexType, vertexData.Length);
		for (int i = 0; i < vertexData.Length; i++)
			this.VertexData.SetValue(vertexData.GetValue(i)!, i);

		this.IndexData = this.IndexType switch 
		{
			IndexType.UInt8 => Array.CreateInstance(typeof(byte), indexData.Length),
			IndexType.UInt16 => Array.CreateInstance(typeof(ushort), indexData.Length),
			IndexType.UInt32 => Array.CreateInstance(typeof(uint), indexData.Length),
			_ => throw new InvalidOperationException("IndexType must be UInt8, UInt16 or UInt32.")
		};

		for (int i = 0; i < indexData.Length; i++)
			this.IndexData.SetValue(i, this.IndexType switch 
				{
					IndexType.UInt8 => ((IConvertible)indexData.GetValue(i)!).ToByte(null),
					IndexType.UInt16 => ((IConvertible)indexData.GetValue(i)!).ToUInt16(null),
					IndexType.UInt32 => ((IConvertible)indexData.GetValue(i)!).ToUInt32(null),
					_ => throw new InvalidOperationException("IndexType must be UInt8, UInt16 or UInt32.")
				}
			);

		vk.CreateVertexBuffer(this.VertexData, out this.VertexBuffer, out this.VertexBufferMemory);
		vk.CreateIndexBuffer(this.IndexData, out this.IndexBuffer, out this.IndexBufferMemory);
	}

	protected Mesh(Vulkan.Program vk, Type vertexType, Type? indexType, string filename) : this(vertexType, indexType)
	{
		if (vk == null || filename == null)
			throw new ArgumentNullException();

		if (!File.Exists(filename))
			throw new FileNotFoundException();

		var extension = Path.GetExtension(filename).ToLower();

		switch (extension) 
		{
			case ".obj":
				var obj = OBJ.FromFile(filename);

				this.VertexData = Array.CreateInstance(this.VertexType, obj.Vertices.Count);

				if (indexType == null) 
				{
					this.IndexType = obj.Indices.Max() switch 
					{
						<= byte.MaxValue => IndexType.UInt8,
						> byte.MaxValue and <= ushort.MaxValue => IndexType.UInt16,
						> ushort.MaxValue and <= uint.MaxValue => IndexType.UInt32
					};
				}

				for (int i = 0; i < this.VertexData.Length; i++) 
				{
					var v = this.VertexData.GetValue(i);

					((IVertex)v!).Position = (obj.Vertices.Count != 0) ? obj.Vertices[i] : default;
					((IVertex)v!).UV = (obj.Textures.Count != 0) ? obj.Textures[i] : default;
					((IVertex)v!).Normal = (obj.Normals.Count != 0) ? obj.Normals[i] : default;

					this.VertexData.SetValue(v, i);
				}

				this.IndexData = this.IndexType switch 
				{
					IndexType.UInt8 => obj.Indices.Select(x => checked((byte)x)).ToArray(),
					IndexType.UInt16 => obj.Indices.Select(x => checked((ushort)x)).ToArray(),
					IndexType.UInt32 => obj.Indices.Select(x => checked((uint)x)).ToArray(),
					_ => throw new InvalidOperationException("IndexType must be UInt8, UInt16 or UInt32.")
				};
				break;
			default:
				throw new InvalidOperationException($"Failed to parse mesh of type '{extension}'.");
		}

		vk.CreateVertexBuffer(this.VertexData, out this.VertexBuffer, out this.VertexBufferMemory);
		vk.CreateIndexBuffer(this.IndexData, out this.IndexBuffer, out this.IndexBufferMemory);
	}
}

public sealed class Mesh<TVertex> : Mesh
{
	public Mesh(Vulkan.Program vk, string filename) : base(vk, typeof(TVertex), null, filename) {}
	public Mesh(Vulkan.Program vk, Array vertexData, Array indexData) : base(vk, typeof(TVertex), null, vertexData, indexData) {}
}

public sealed class Mesh<TVertex, TIndex> : Mesh
{
	public Mesh(Vulkan.Program vk, string filename) : base(vk, typeof(TVertex), typeof(TIndex), filename) {}
	public Mesh(Vulkan.Program vk, Array vertexData, Array indexData) : base(vk, typeof(TVertex), typeof(TIndex), vertexData, indexData) {}
}

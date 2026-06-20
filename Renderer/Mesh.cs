using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using Vulkan;

namespace Renderer;

public class Mesh
{
	public Type VertexType { get; }
	public Array Vertices { get; }
	public int VertexCount => this.Vertices.Length;

	public IndexType IndexType { get; }
	public Array Indices { get; }
	public int IndexCount => this.Indices.Length;

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

	public Mesh() : this(typeof(DefaultVertex), typeof(byte), new DefaultVertex[] { default }, new byte[] { 0, 0, 0 }) { }

	protected Mesh(Type vertexType, Type? indexType, Array vertexData, Array indexData) : this(vertexType, indexType)
	{
		if (vertexData == null || indexData == null)
			throw new ArgumentNullException();

		if (vertexData.Length == 0)
			throw new ArgumentOutOfRangeException(nameof(vertexData), "Vertex array must have at least one vertex.");

		Type vertexDataType = vertexData.GetValue(0)!.GetType();
		if (vertexDataType != vertexType)
			throw new ArgumentException($"Vertex array elements must be '{vertexType}', not '{vertexDataType}'.");

		if (indexData.Length == 0 || indexData.Length % 3 != 0)
			throw new ArgumentOutOfRangeException(nameof(indexData), "Indices count must be multiple of 3.");

		Type indexDataType = indexData.GetValue(0)!.GetType();
		if (indexDataType != typeof(byte) && indexDataType != typeof(ushort) && indexDataType != typeof(uint))
			throw new ArgumentException($"Index array elements must be byte, ushort or uint, not '{indexDataType}'.");

		uint maxIndex = 0;
		for (int i = 0; i < indexData.Length; i++)
			maxIndex = Math.Max(maxIndex, Convert.ToUInt32(indexData.GetValue(i), null));

		if (maxIndex >= vertexData.Length)
			throw new ArgumentException("Index array references a vertex out of bounds of the vertex array.");

		if (indexType == null)
		{
			this.IndexType = maxIndex switch
			{
				<= byte.MaxValue => IndexType.UInt8,
				> byte.MaxValue and <= ushort.MaxValue => IndexType.UInt16,
				> ushort.MaxValue and <= uint.MaxValue => IndexType.UInt32
			};
		}

		this.Vertices = Array.CreateInstance(this.VertexType, vertexData.Length);
		Array.Copy(vertexData, this.Vertices, vertexData.Length);

		indexDataType = this.IndexType switch
		{
			IndexType.UInt8 => typeof(byte),
			IndexType.UInt16 => typeof(ushort),
			IndexType.UInt32 => typeof(uint),
			_ => throw new InvalidOperationException("IndexType must be UInt8, UInt16 or UInt32.")
		};
		this.Indices = Array.CreateInstance(indexDataType, indexData.Length);
		for (int i = 0; i < indexData.Length; i++)
			this.Indices.SetValue(Convert.ChangeType(this.IndexType switch
			{
				IndexType.UInt8 => Convert.ToByte(indexData.GetValue(i), null),
				IndexType.UInt16 => Convert.ToUInt16(indexData.GetValue(i), null),
				IndexType.UInt32 => Convert.ToUInt32(indexData.GetValue(i), null),
				_ => throw new InvalidOperationException("IndexType must be UInt8, UInt16 or UInt32.")
			},
					indexDataType
				),
				i
			);
	}

	protected Mesh(Type vertexType, Type? indexType, string filename) : this(vertexType, indexType)
	{
		if (filename == null)
			throw new ArgumentNullException();

		if (!File.Exists(filename))
			throw new FileNotFoundException();

		var extension = Path.GetExtension(filename).ToLower();

		switch (extension)
		{
			case ".obj":
				var obj = OBJ.FromFile(filename);

				this.Vertices = Array.CreateInstance(this.VertexType, obj.Vertices.Count);

				if (indexType == null)
				{
					this.IndexType = obj.Indices.Max() switch
					{
						<= byte.MaxValue => IndexType.UInt8,
						> byte.MaxValue and <= ushort.MaxValue => IndexType.UInt16,
						> ushort.MaxValue and <= uint.MaxValue => IndexType.UInt32
					};
				}

				for (int i = 0; i < this.Vertices.Length; i++)
				{
					var v = this.Vertices.GetValue(i);

					((IVertex)v!).Position = (obj.Vertices.Count != 0) ? obj.Vertices[i] : default;
					((IVertex)v!).UV = (obj.Textures.Count != 0) ? obj.Textures[i] : default;
					((IVertex)v!).Normal = (obj.Normals.Count != 0) ? obj.Normals[i] : default;

					this.Vertices.SetValue(v, i);
				}

				this.Indices = this.IndexType switch
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
	}
}

public class Mesh<TVertex> : Mesh
{
	public Mesh(string filename) : base(typeof(TVertex), null, filename) { }
	public Mesh(Array vertexData, Array indexData) : base(typeof(TVertex), null, vertexData, indexData) { }
}

public class Mesh<TVertex, TIndex> : Mesh
{
	public Mesh(string filename) : base(typeof(TVertex), typeof(TIndex), filename) { }
	public Mesh(Array vertexData, Array indexData) : base(typeof(TVertex), typeof(TIndex), vertexData, indexData) { }
}

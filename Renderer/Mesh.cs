using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using Vulkan;

namespace Renderer;

public sealed class Mesh : IDisposable
{
	internal readonly Vulkan.Buffer VertexBuffer;
	internal readonly DeviceMemory VertexBufferMemory;
	internal readonly int VertexCount;
	internal readonly Type VertexType;

	internal readonly Vulkan.Buffer IndexBuffer;
	internal readonly DeviceMemory IndexBufferMemory;
	internal readonly int IndexCount;
	internal readonly IndexType IndexType;

	public void Dispose() 
	{
		VertexBuffer.Dispose();
		VertexBufferMemory.Dispose();
		IndexBuffer.Dispose();
		IndexBufferMemory.Dispose();
	}

	public Mesh(Vulkan.Program vk, Type vertexType, string filename) 
	{
		Array vertexData, indexData;

		var extension = Path.GetExtension(filename).ToLower();
		switch (extension) 
		{
			case ".obj":
				var obj = OBJ.FromFile(filename);

				vertexData = Array.CreateInstance(vertexType, obj.Vertices.Count);

				this.IndexCount = obj.Indices.Count;
				this.IndexType = obj.Indices.Max() switch 
				{
					<= byte.MaxValue => IndexType.UInt8,
					> byte.MaxValue and <= ushort.MaxValue => IndexType.UInt16,
					> ushort.MaxValue and <= uint.MaxValue => IndexType.UInt32
				};

				for (int i = 0; i < vertexData.Length; i++) 
				{
					var v = vertexData.GetValue(i);

					((IVertex)v!).Position = (obj.Vertices.Count != 0) ? obj.Vertices[i] : default;
					((IVertex)v!).UV = (obj.Textures.Count != 0) ? obj.Textures[i] : default;
					((IVertex)v!).Normal = (obj.Normals.Count != 0) ? obj.Normals[i] : default;

					vertexData.SetValue(v, i);
				}

				indexData = this.IndexType switch 
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

		this.VertexType = vertexType;
		this.VertexCount = vertexData.Length;

		vk.CreateVertexBuffer(vertexData, out this.VertexBuffer, out this.VertexBufferMemory);
		vk.CreateIndexBuffer(indexData, out this.IndexBuffer, out this.IndexBufferMemory);
	}
}

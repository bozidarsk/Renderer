using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using Vulkan;

namespace Renderer;

public sealed class Mesh : Component, IDisposable	
{
	public readonly Vulkan.Buffer VertexBuffer;
	public readonly DeviceMemory VertexBufferMemory;
	public readonly int VertexCount;
	public readonly Type VertexType;

	public readonly Vulkan.Buffer IndexBuffer;
	public readonly DeviceMemory IndexBufferMemory;
	public readonly int IndexCount;
	public readonly IndexType IndexType;

	public void Dispose() 
	{
		VertexBuffer.Dispose();
		VertexBufferMemory.Dispose();
		IndexBuffer.Dispose();
		IndexBufferMemory.Dispose();
	}

	public Mesh(Vulkan.Program vk, Type vertexType, OBJ obj) 
	{
		if (!vertexType.IsValueType || !vertexType.IsAssignableTo(typeof(IVertex)))
			throw new ArgumentException($"Vertex type '{vertexType.FullName}' must be a struct and implement IVertex.");

		var vertexData = Array.CreateInstance(vertexType, obj.Vertices.Count);

		this.VertexType = vertexType;
		this.VertexCount = vertexData.Length;
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

		vk.CreateVertexBuffer(vertexData, out this.VertexBuffer, out this.VertexBufferMemory);
		vk.CreateIndexBuffer(
			this.IndexType switch 
			{
				IndexType.UInt8 => obj.Indices.Select(x => checked((byte)x)).ToArray(),
				IndexType.UInt16 => obj.Indices.Select(x => checked((ushort)x)).ToArray(),
				IndexType.UInt32 => obj.Indices.Select(x => checked((uint)x)).ToArray(),
				_ => throw new InvalidOperationException("IndexType must be UInt8, UInt16 or UInt32.")
			},
			out this.IndexBuffer,
			out this.IndexBufferMemory
		);
	}
}

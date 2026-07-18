using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Renderer;

public class Buffer
{
	public Array Data { get; }
	public int Length { get; }
	public int Stride { get; }
	public int Size { get; }

	internal protected Buffer(Array data)
	{
		this.Data = data;

		this.Length = data.Length;
		this.Stride = data.Length * Marshal.SizeOf(data.GetType().GetElementType()!);
		this.Size = this.Length * this.Stride;
	}
}

public class Buffer<T> : Buffer, IEnumerable<T> where T : struct
{
	new public T[] Data => (T[])base.Data;

	public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Data).GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => Data.GetEnumerator();

	public Buffer(int length) : base(new T[length])
	{
	}

	public Buffer(T[] data) : base(data ?? throw new ArgumentNullException())
	{
	}
}

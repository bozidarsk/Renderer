using System.IO;

namespace Renderer;

public static class StreamExtensions 
{
	public static byte[] ReadBytes(this Stream reader, int length) 
	{
		var bytes = new byte[length];
		reader.ReadExactly(bytes, 0, length);
		return bytes;
	}

	public static void Write(this Stream writer, byte x) => writer.WriteByte(x);

	public static void Write(this Stream writer, ushort x) 
	{
		writer.WriteByte((byte)(x >> 0));
		writer.WriteByte((byte)(x >> 8));
	}

	public static void Write(this Stream writer, uint x) 
	{
		writer.WriteByte((byte)(x >> 0));
		writer.WriteByte((byte)(x >> 8));
		writer.WriteByte((byte)(x >> 16));
		writer.WriteByte((byte)(x >> 24));
	}

	public static void Write(this Stream writer, ulong x) 
	{
		writer.WriteByte((byte)(x >> 0));
		writer.WriteByte((byte)(x >> 8));
		writer.WriteByte((byte)(x >> 16));
		writer.WriteByte((byte)(x >> 24));
		writer.WriteByte((byte)(x >> 32));
		writer.WriteByte((byte)(x >> 40));
		writer.WriteByte((byte)(x >> 48));
		writer.WriteByte((byte)(x >> 56));
	}

	public static void WriteBigEndian(this Stream writer, ushort x) 
	{
		writer.WriteByte((byte)(x >> 8));
		writer.WriteByte((byte)(x >> 0));
	}

	public static void WriteBigEndian(this Stream writer, uint x) 
	{
		writer.WriteByte((byte)(x >> 24));
		writer.WriteByte((byte)(x >> 16));
		writer.WriteByte((byte)(x >> 8));
		writer.WriteByte((byte)(x >> 0));
	}

	public static void WriteBigEndian(this Stream writer, ulong x) 
	{
		writer.WriteByte((byte)(x >> 56));
		writer.WriteByte((byte)(x >> 48));
		writer.WriteByte((byte)(x >> 40));
		writer.WriteByte((byte)(x >> 32));
		writer.WriteByte((byte)(x >> 24));
		writer.WriteByte((byte)(x >> 16));
		writer.WriteByte((byte)(x >> 8));
		writer.WriteByte((byte)(x >> 0));
	}

	public static byte ReadUInt8(this Stream stream) => (byte)stream.ReadByte();

	public static ushort ReadUInt16(this Stream reader) 
	{
		ushort x = 0;
		x |= (ushort)((int)reader.ReadByte() << 0);
		x |= (ushort)((int)reader.ReadByte() << 8);
		return x;
	}

	public static uint ReadUInt32(this Stream reader) 
	{
		uint x = 0;
		x |= (uint)reader.ReadByte() << 0;
		x |= (uint)reader.ReadByte() << 8;
		x |= (uint)reader.ReadByte() << 16;
		x |= (uint)reader.ReadByte() << 24;
		return x;
	}

	public static ulong ReadUInt64(this Stream reader) 
	{
		ulong x = 0;
		x |= (ulong)reader.ReadByte() << 0;
		x |= (ulong)reader.ReadByte() << 8;
		x |= (ulong)reader.ReadByte() << 16;
		x |= (ulong)reader.ReadByte() << 24;
		x |= (ulong)reader.ReadByte() << 32;
		x |= (ulong)reader.ReadByte() << 40;
		x |= (ulong)reader.ReadByte() << 48;
		x |= (ulong)reader.ReadByte() << 56;
		return x;
	}

	public static ushort ReadUInt16BigEndian(this Stream reader) 
	{
		ushort x = 0;
		x |= (ushort)((int)reader.ReadByte() << 8);
		x |= (ushort)((int)reader.ReadByte() << 0);
		return x;
	}

	public static uint ReadUInt32BigEndian(this Stream reader) 
	{
		uint x = 0;
		x |= (uint)reader.ReadByte() << 24;
		x |= (uint)reader.ReadByte() << 16;
		x |= (uint)reader.ReadByte() << 8;
		x |= (uint)reader.ReadByte() << 0;
		return x;
	}

	public static ulong ReadUInt64BigEndian(this Stream reader) 
	{
		ulong x = 0;
		x |= (ulong)reader.ReadByte() << 56;
		x |= (ulong)reader.ReadByte() << 48;
		x |= (ulong)reader.ReadByte() << 40;
		x |= (ulong)reader.ReadByte() << 32;
		x |= (ulong)reader.ReadByte() << 24;
		x |= (ulong)reader.ReadByte() << 16;
		x |= (ulong)reader.ReadByte() << 8;
		x |= (ulong)reader.ReadByte() << 0;
		return x;
	}
}

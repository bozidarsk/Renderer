using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Hashing;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Renderer;

public sealed class PNG : Texture
{
	private const ulong SIGNATURE = 0x89504e470d0a1a0a;

	new public static PNG FromFile(string filename) 
	{
		if (filename == null)
			throw new ArgumentNullException();

		if (!File.Exists(filename))
			throw new FileNotFoundException();

		return new(filename);
	}

	public override void Save(string filename) 
	{
		if (filename == null)
			throw new ArgumentNullException();

		using var writer = File.OpenWrite(filename);
		writer.WriteBigEndian(SIGNATURE);

		{
			using var chunk = new MemoryStream();
			chunk.WriteBigEndian(base.Width);
			chunk.WriteBigEndian(base.Height);
			chunk.Write((byte)base.Depth);
			chunk.Write((byte)(ColorType.Color | ColorType.Alpha));
			chunk.Write((byte)CompressionType.Deflate);
			chunk.Write((byte)FilterType.None);
			chunk.Write((byte)0); // interlaced

			WriteChunk(writer, ChunkType.IHDR, chunk, false);
		}

		{
			using var chunk = new MemoryStream();
			for (uint y = 0; y < base.Height; y++) 
			{
				chunk.Write((byte)FilterType.None);

				for (uint x = 0; x < base.Width; x++)
					chunk.WriteBigEndian(base.Colors[y * base.Width + x]);
			}

			WriteChunk(writer, ChunkType.IDAT, chunk, true);
		}

		{
			using var chunk = new MemoryStream();

			WriteChunk(writer, ChunkType.IEND, chunk, false);
		}
	}

	private void WriteChunk(Stream writer, ChunkType type, Stream data, bool compress) 
	{
		data.Position = 0;

		using var finalData = !compress ? data : new MemoryStream();

		if (compress) 
		{
			using var zlib = new ZLibStream(finalData, CompressionMode.Compress, leaveOpen: true);
			data.CopyTo(zlib);
		}

		writer.WriteBigEndian((uint)finalData.Length);
		writer.WriteBigEndian((uint)type);

		finalData.Position = 0;
		finalData.CopyTo(writer);

		var crc32 = new Crc32();
		crc32.Append(
			[
				(byte)((uint)type >> 24),
				(byte)((uint)type >> 16),
				(byte)((uint)type >> 8),
				(byte)((uint)type >> 0)
			]
		);

		finalData.Position = 0;
		crc32.Append(finalData);

		writer.WriteBigEndian(crc32.GetCurrentHashAsUInt32());
	}

	private Stream ReadChunk(Stream reader, out ChunkType type, out uint length, out uint crc) 
	{
		length = reader.ReadUInt32BigEndian();
		type = (ChunkType)reader.ReadUInt32BigEndian();

		byte[] data = reader.ReadBytes(checked((int)length));

		var crc32 = new Crc32();
		crc32.Append(
			[
				(byte)((uint)type >> 24),
				(byte)((uint)type >> 16),
				(byte)((uint)type >> 8),
				(byte)((uint)type >> 0)
			]
		);
		crc32.Append(data);

		crc = reader.ReadUInt32BigEndian();

		if (crc != crc32.GetCurrentHashAsUInt32())
			throw new FormatException($"Crc32 check does not match for chunk '{type}'.");

		return new MemoryStream(data);
	}

	private static int PaethPredictor(int a, int b, int c) 
	{
		// a = left, b = above, c = upper left
		int p = a + b - c;        // Initial estimate
		int pa = Math.Abs(p - a); // Distance to a
		int pb = Math.Abs(p - b); // Distance to b  
		int pc = Math.Abs(p - c); // Distance to c

		// Return nearest of a, b, c, breaking ties in order a, b, c
		if (pa <= pb && pa <= pc)
			return a;
		else if (pb <= pc)
			return b;
		else
			return c;
	}

	private uint[] ReadTrueColor(Dictionary<ChunkType, Stream> chunks, Stream data) 
	{
		var pixels = new uint[base.Width * base.Height];
		uint stride = (uint)(base.IsTransparent ? 4 : 3);
		byte[]? previousScanline = null;

		for (uint y = 0; y < base.Height; y++) 
		{
			var filterType = (FilterType)data.ReadUInt8();
			byte[] scanline = data.ReadBytes(checked((int)(base.Width * stride)));

			switch (filterType) 
			{
				case FilterType.None:
					break;
				case FilterType.Sub:
					for (uint i = stride; i < scanline.Length; i++)
						 scanline[i] = (byte)(scanline[i] + scanline[i - stride]);
					break;
				case FilterType.Up:
					if (previousScanline != null)
						for (int i = 0; i < scanline.Length; i++)
							scanline[i] = (byte)(scanline[i] + previousScanline[i]);
					break;
				case FilterType.Average:
					for (int i = 0; i < scanline.Length; i++)
					{
						int left = (i >= stride) ? scanline[i - stride] : 0;
						int above = (previousScanline != null) ? previousScanline[i] : 0;
						int average = (left + above) / 2;
						scanline[i] = (byte)(scanline[i] + average);
					}
					break;
				case FilterType.Paeth:
					for (int i = 0; i < scanline.Length; i++)
					{
						int left = (i >= stride) ? scanline[i - stride] : 0;
						int above = (previousScanline != null) ? previousScanline[i] : 0;
						int upperLeft = (i >= stride && previousScanline != null) ? 
						previousScanline[i - stride] : 0;

						int predictor = PaethPredictor(left, above, upperLeft);
						scanline[i] = (byte)(scanline[i] + predictor);
					}
					break;
				default:
					throw new FormatException($"Invalid filter type '{filterType}'.");
			}

			for (uint x = 0; x < base.Width; x++) 
			{
				uint index = x * stride;
				pixels[y * base.Width + x] = 
					(uint)((uint)scanline[index + 0] << 24) | 
					(uint)((uint)scanline[index + 1] << 16) | 
					(uint)((uint)scanline[index + 2] << 8) | 
					(uint)(base.IsTransparent ? scanline[index + 3] : byte.MaxValue)
				;
			}

			previousScanline = scanline;
		}

		return pixels;
	}

	private uint[] ReadIndexed(Dictionary<ChunkType, Stream> chunks, Stream data) 
	{
		if (base.Depth != 1 && base.Depth != 2 && base.Depth != 4 && base.Depth != 8)
			throw new FormatException($"Invalid depth '{base.Depth}'.");

		Stream plte = chunks[ChunkType.PLTE];
		Stream? trns = null;
		int mask = (1 << (int)base.Depth) - 1;

		if (base.IsTransparent)
			trns = chunks[ChunkType.tRNS];

		var pixels = new uint[base.Width * base.Height];
		uint stride = 1;
		byte[]? previousScanline = null;

		for (uint y = 0; y < base.Height; y++) 
		{
			var filterType = (FilterType)data.ReadUInt8();
			byte[] scanline = data.ReadBytes(checked((int)(base.Width / (8 / base.Depth))));

			switch (filterType) 
			{
				case FilterType.None:
					break;
				case FilterType.Sub:
					for (uint i = stride; i < scanline.Length; i++)
						 scanline[i] = (byte)(scanline[i] + scanline[i - stride]);
					break;
				case FilterType.Up:
					if (previousScanline != null)
						for (int i = 0; i < scanline.Length; i++)
							scanline[i] = (byte)(scanline[i] + previousScanline[i]);
					break;
				case FilterType.Average:
					for (int i = 0; i < scanline.Length; i++)
					{
						int left = (i >= stride) ? scanline[i - stride] : 0;
						int above = (previousScanline != null) ? previousScanline[i] : 0;
						int average = (left + above) / 2;
						scanline[i] = (byte)(scanline[i] + average);
					}
					break;
				case FilterType.Paeth:
					for (int i = 0; i < scanline.Length; i++)
					{
						int left = (i >= stride) ? scanline[i - stride] : 0;
						int above = (previousScanline != null) ? previousScanline[i] : 0;
						int upperLeft = (i >= stride && previousScanline != null) ? 
						previousScanline[i - stride] : 0;

						int predictor = PaethPredictor(left, above, upperLeft);
						scanline[i] = (byte)(scanline[i] + predictor);
					}
					break;
				default:
					throw new FormatException($"Invalid filter type '{filterType}'.");
			}

			for (uint x = 0; x < base.Width; x++) 
			{
				int ppb = 8 / (int)base.Depth;
				long index = x / ppb;
				byte a = byte.MaxValue;

				plte.Position = ((long)scanline[index] >> (int)(base.Depth * (ppb - (x % ppb) - 1))) & mask;
				plte.Position *= 3;

				if (base.IsTransparent) 
				{
					trns!.Position = plte.Position;
					a = (trns.Position < trns.Length) ? trns.ReadUInt8() : byte.MaxValue;
				}

				byte r = plte.ReadUInt8();
				byte g = plte.ReadUInt8();
				byte b = plte.ReadUInt8();

				pixels[y * base.Width + x] = ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | (uint)a;
			}

			previousScanline = scanline;
		}

		return pixels;
	}

	private unsafe PNG(string filename) 
	{
		using var reader = File.OpenRead(filename);

		if (reader.ReadUInt64BigEndian() != SIGNATURE)
			throw new FormatException("Invalid signature.");

		(
			uint width,
			uint height,
			byte depth,
			ColorType colorType,
			CompressionType compressionType,
			FilterType filterType,
			bool interlaced
		) = (default, default, default, default, default, default, default);

		var chunks = new Dictionary<ChunkType, Stream>();

		for (
			Stream data = ReadChunk(reader, out ChunkType chunkType, out uint length, out uint crc);
			chunkType != ChunkType.IEND;
			data = ReadChunk(reader, out chunkType, out length, out crc)
		)
		{
			if (chunkType == ChunkType.IHDR) 
			{
				width = data.ReadUInt32BigEndian();
				height = data.ReadUInt32BigEndian();
				depth = data.ReadUInt8();
				colorType = (ColorType)data.ReadUInt8();
				compressionType = (CompressionType)data.ReadUInt8();
				filterType = (FilterType)data.ReadUInt8();
				interlaced = data.ReadUInt8() == 1;
				continue;
			}

			if (!chunks.ContainsKey(chunkType))
				chunks[chunkType] = new MemoryStream();

			data.CopyTo(chunks[chunkType]);
		}

		foreach ((var key, var value) in chunks)
			value.Position = 0;

		using var zlib = new ZLibStream(chunks[ChunkType.IDAT], CompressionMode.Decompress);
		using var decompressed = new MemoryStream();
		zlib.CopyTo(decompressed);
		decompressed.Position = 0;

		base.Width = width;
		base.Height = height;
		base.Depth = (uint)depth;
		base.IsTransparent = colorType.HasFlag(ColorType.Alpha);
		base.Colors = colorType switch 
		{
			ColorType.Color => ReadTrueColor(chunks, decompressed),
			ColorType.Color | ColorType.Alpha => ReadTrueColor(chunks, decompressed),
			ColorType.Color | ColorType.Palette => ReadIndexed(chunks, decompressed),
			ColorType.Color | ColorType.Palette | ColorType.Alpha => ReadIndexed(chunks, decompressed),
			_ => throw new FormatException($"Invalid color type '{colorType}'.")
		};;

		foreach ((var key, var value) in chunks)
			value.Dispose();
	}

	private enum ChunkType : uint
	{
		IHDR = 0x49484452,
		IDAT = 0x49444154,
		IEND = 0x49454e44,
		PLTE = 0x504c5445,
		bKGD = 0x624b4744,
		cHRM = 0x6348524d,
		dSIG = 0x64534947,
		eXIf = 0x65584966,
		gAMA = 0x67414d41,
		hIST = 0x68495354,
		iCCP = 0x69434350,
		iTXt = 0x69545874,
		pHYs = 0x70485973,
		sBIT = 0x73424954,
		sPLT = 0x73504c54,
		sRGB = 0x73524742,
		sTER = 0x73544552,
		tEXt = 0x74455874,
		tIME = 0x74494d45,
		tRNS = 0x74524e53,
		zTXt = 0x7a545874,
	}

	[Flags]
	private enum ColorType : byte
	{
		Grayscale = 0,
		Palette = 1,
		Color = 2,
		Alpha = 4,
	}

	private enum CompressionType : byte
	{
		Deflate = 0
	}

	private enum FilterType : byte
	{
		None = 0, // Zero (so that the raw byte value passes through unaltered)
		Sub = 1, // Byte A (to the left)
		Up = 2, // Byte B (above)
		Average = 3, // Mean of bytes A and B, rounded down
		Paeth = 4, // A, B, or C, whichever is closest to p = A + B − C (top-left)
	}
}

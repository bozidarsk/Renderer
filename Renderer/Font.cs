using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

using Vulkan;
using Clipper2Lib;

namespace Renderer;

internal struct OuterTextVertex : IVertex
{
	public Vector3 Position { set; get; }
	public Vector2 UV { set; get; }
	public Vector3 Normal { set; get; }
}

internal struct InnerTextVertex : IVertex
{
	public Vector3 Position { set; get; }
	public Vector2 UV { set {} }
	public Vector3 Normal { set {} }
}

internal sealed record TextMesh(Mesh<OuterTextVertex> Outer, Mesh<InnerTextVertex> Inner);

public sealed class Font 
{
	private readonly Glyph[] glyphs;
	private readonly Dictionary<uint, int> map;
	private readonly float scale;

	private static string[] searchDirectories = 
	[
	#if LINUX
		"/usr/share/fonts",
		"/usr/local/share/fonts",
		$"{Environment.GetEnvironmentVariable("HOME")}/.fonts"
	#elif WINDOWS
		@"c:\Windows\Fonts"
	#elif MAC
		#error Not implemented. (MAC)
	#else
		#error Unknown os.
	#endif
	];

	internal TextMesh CreateMesh(Vulkan.Program vk, string text) 
	{
		if (text == null)
			throw new ArgumentNullException();

		Span<byte> unicode = stackalloc byte[text.Length * 2];
		int length = Encoding.Unicode.GetBytes(text, unicode);

		if (length % 2 != 0)
			throw new NotImplementedException();

		Span<int> indices = stackalloc int[length / 2];
		for (int i = 0; i < indices.Length; i++)
			indices[i] = map[(uint)unicode[i * 2] | (uint)((uint)unicode[i * 2 + 1] << 8)];

		return CreateMesh(vk, indices);
	}

	private TextMesh CreateMesh(Vulkan.Program vk, Span<int> glyphIndices) 
	{
		var outerVertices = new List<OuterTextVertex>();
		var innerVertices = new List<InnerTextVertex>();
		var outerIndices = new List<uint>();
		var innerIndices = new List<uint>();

		var offset = Vector3.Zero;

		for (int i = 0; i < glyphIndices.Length; i++) 
		{
			var g = glyphs[glyphIndices[i]];

			{
				uint count = (uint)outerVertices.Count;
				(var vertices, var indices) = CreateOuterMesh(g, offset, scale);
				outerVertices.AddRange(vertices);
				outerIndices.AddRange(indices.Select(x => (uint)x + count));
			}

			{
				uint count = (uint)innerVertices.Count;
				(var vertices, var indices) = CreateInnerMesh(g, offset, scale);
				innerVertices.AddRange(vertices);
				innerIndices.AddRange(indices.Select(x => (uint)x + count));
			}

			offset.x += g.Spacing * scale;
		}

		return new(
			new Mesh<OuterTextVertex>(vk, outerVertices.ToArray(), outerIndices.ToArray()),
			new Mesh<InnerTextVertex>(vk, innerVertices.ToArray(), innerIndices.ToArray())
		);
	}

	private static (IEnumerable<OuterTextVertex>, IEnumerable<int>) CreateOuterMesh(Glyph g, Vector3 offset, float scale) 
	{
		var vertices = new OuterTextVertex[g.OuterIndices.Length];
		var indices = new int[g.OuterIndices.Length];

		for (int i = 0; i < g.OuterIndices.Length - 2; i += 3) 
		{
			vertices[i + 0] = new OuterTextVertex() 
			{
				Position = new Vector3(g.Vertices[g.OuterIndices[i + 0]].x, g.Vertices[g.OuterIndices[i + 0]].y, 0) * scale + offset,
				UV = new(0, 0),
				Normal = Vector3.Forward
			};

			vertices[i + 1] = new OuterTextVertex() 
			{
				Position = new Vector3(g.Vertices[g.OuterIndices[i + 1]].x, g.Vertices[g.OuterIndices[i + 1]].y, 0) * scale + offset,
				UV = new(0.5f, 0),
				Normal = Vector3.Forward
			};

			vertices[i + 2] = new OuterTextVertex() 
			{
				Position = new Vector3(g.Vertices[g.OuterIndices[i + 2]].x, g.Vertices[g.OuterIndices[i + 2]].y, 0) * scale + offset,
				UV = new(1, 1),
				Normal = Vector3.Forward
			};

			indices[i + 0] = i + 0;
			indices[i + 1] = i + 1;
			indices[i + 2] = i + 2;
		}

		return (vertices, indices);
	}

	private static (IEnumerable<InnerTextVertex>, IEnumerable<int>) CreateInnerMesh(Glyph g, Vector3 offset, float scale) 
	{
		return (g.Vertices.Select(x => new InnerTextVertex() { Position = new Vector3(x.x, x.y, 0) * scale + offset }), g.InnerIndices);
	}

	private static string? Search(string filename) 
	{
		if (Path.GetExtension(filename) == "")
			filename += ".ttf";

		foreach (var directory in searchDirectories) 
		{
			string? found = Directory.EnumerateFiles(directory, filename, SearchOption.AllDirectories).FirstOrDefault();

			if (found != null)
				return found;
		}

		return null;
	}

	private static Table ReadTable(Stream reader) 
	{
		Tag tag = (Tag)reader.ReadUInt32BigEndian();
		uint checkSum = reader.ReadUInt32BigEndian();
		uint offset = reader.ReadUInt32BigEndian();
		uint length = reader.ReadUInt32BigEndian();
		return new(tag, checkSum, offset, length);
	}

	private static void Triangulate(List<Vector2> vertices, List<int> outerIndices, List<int> innerIndices) 
	{
		var innerContours = new List<(int start, int end, bool isClockwise)>();
		var outerContours = new List<(int start, int end, bool isClockwise)>();

		for ((int start, int end) = (0, 0); end < innerIndices.Count; end += 2) 
		{
			while (innerIndices[end++] != -1);
			end -= 2;

			innerContours.Add((start, end, isClockwise(innerIndices, start, end)));

			start = end + 2;
		}

		for ((int start, int end) = (0, 0); end < outerIndices.Count; end += 2) 
		{
			while (outerIndices[end++] != -1);
			end -= 2;

			outerContours.Add((start, end, isClockwise(outerIndices, start, end)));

			start = end + 2;
		}

		var clipper = new ClipperD(6);
		var solution = new PolyTreeD();

		foreach (var indices in innerContours.Select(x => innerIndices.Take(new Range(x.start, x.end + 1)))) 
		{
			clipper.AddSubject(
				Clipper.MakePath(
					indices
					.Select(x => vertices[x])
					.Select<Vector2, double[]>(x => [ (double)x.x, (double)x.y ])
					.SelectMany(x => x)
					.ToArray()
				)
			);
		}

		foreach (var indices in outerContours.Select(x => outerIndices.Take(new Range(x.start, x.end + 1)))) 
		{
			clipper.AddClip(
				Clipper.MakePath(
					indices
					.Select(x => vertices[x])
					.Select<Vector2, double[]>(x => [ (double)x.x, (double)x.y ])
					.SelectMany(x => x)
					.ToArray()
				)
			);
		}

		clipper.Execute(ClipType.Intersection, FillRule.NonZero, solution);

		var polygons = new List<List<int>>();
		var holes = new List<List<int>>();

		// !!!!!! TODO: will fail if there is a polygon inside a hole inside a polygon !!!!!!
		foreach ((var path, var isHole) in getPaths(solution)) 
		{
			if (!isHole)
				polygons.Add(new(getIndices(path)));
			else
				holes.Add(new(getIndices(path)));
		}

		innerIndices.Clear();

		try 
		{
			foreach (var x in polygons)
				innerIndices.AddRange(EarClipping.Triangulate(vertices, x, holes, isClockwise: false));
		}
		catch (InvalidOperationException) 
		{
			Console.WriteLine("Failed to triangulate a font glyph:");
			Console.WriteLine($"polygons: {polygons.Count}");
			Console.WriteLine($"holes: {holes.Count}");
			Console.WriteLine($"innerContours: {innerContours.Count}");
			Console.WriteLine($"outerContours: {outerContours.Count}");

			foreach (var indices in innerContours.Select(x => innerIndices.Take(new Range(x.start, x.end + 1)))) 
				Console.WriteLine($"i_{{inner}}=[ {indices.Select(x => x.ToString()).Aggregate((current, next) => $"{current}, {next}")} ]");

			foreach (var indices in outerContours.Select(x => outerIndices.Take(new Range(x.start, x.end + 1)))) 
				Console.WriteLine($"i_{{outer}}=[ {indices.Select(x => x.ToString()).Aggregate((current, next) => $"{current}, {next}")} ]");

			Console.WriteLine($"v = [ {vertices.Select(v => $"({v.x}, {v.y})").Aggregate((current, next) => $"{current}, {next}")} ]");

			foreach (var p in polygons)
				Console.WriteLine($"p = [ {p.Select(x => x.ToString()).Aggregate((current, next) => $"{current}, {next}")} ]");

			foreach (var h in holes)
				Console.WriteLine($"h = [ {h.Select(x => x.ToString()).Aggregate((current, next) => $"{current}, {next}")} ]");
		}

		outerIndices.RemoveAll(x => x == -1);

		if (outerIndices.Count % 3 != 0)
			throw new InvalidOperationException("Outer indices count must be multiple of 3.");

		if (innerIndices.Count % 3 != 0)
			throw new InvalidOperationException("Inner indices count must be multiple of 3.");

		IEnumerable<(PathD path, bool isHole)> getPaths(PolyPathD current) 
		{
			if (current.Polygon != null)
				yield return (current.Polygon, current.IsHole);

			for (int i = 0; i < current.Count; i++) 
			{
				foreach (var x in getPaths(current[i]))
					yield return x;
			}
		}

		IEnumerable<int> getIndices(PathD path) 
		{
			foreach (var point in path) 
			{
				var x = new Vector2((float)point.x, (float)point.y);
				int index = vertices.IndexOf(x);

				if (index == -1) 
				{
					vertices.Add(x);
					index = vertices.Count - 1;
				}

				yield return index;
			}
		}

		bool isClockwise(List<int> indices, int start, int end) 
		{
			float area = 0;
			int n = end - start + 1;

			for (int i = 0; i < n; i++) 
			{
				int j = (i + 1) % n;

				var ii = vertices[indices[start + i]];
				var jj = vertices[indices[start + j]];

				area += ii.x * jj.y;
				area -= jj.x * ii.y;
			}

			return area < 0;
		}
	}

	private static Glyph CreateGlyph(Span<int> xcoords, Span<int> ycoords, Span<OutlineFlags> flags, Span<int> contours, int spacing) 
	{
		if (xcoords.Length != ycoords.Length || xcoords.Length != flags.Length)
			throw new ArgumentOutOfRangeException();

		var vertices = new List<Vector2>(xcoords.Length);
		var outerIndices = new List<int>();
		var innerIndices = new List<int>();

		int start = 0;

		for (int i = 0; i < xcoords.Length; i++)
			vertices.Add(new((float)xcoords[i], (float)ycoords[i]));

		foreach (var end in contours) 
		{
			int pointOffset, length = end - start + 1;

			for (pointOffset = 0; pointOffset < length; pointOffset++)
				if (flags[start + pointOffset].HasFlag(OutlineFlags.OnCurve))
					break;

			for (int i = 0; i < length + 1; i++) 
			{
				int current = start + ((i + pointOffset) % length);
				int next = start + ((i + pointOffset + 1) % length);

				outerIndices.Add(current);

				if (current != start && flags[current].HasFlag(OutlineFlags.OnCurve))
					outerIndices.Add(current);

				if (flags[current].HasFlag(OutlineFlags.OnCurve))
					innerIndices.Add(current);

				if (i < length && flags[current].HasFlag(OutlineFlags.OnCurve) == flags[next].HasFlag(OutlineFlags.OnCurve)) 
				{
					vertices.Add((vertices[current] + vertices[next]) / 2f);

					outerIndices.Add(vertices.Count - 1);

					if (!flags[current].HasFlag(OutlineFlags.OnCurve))
						outerIndices.Add(vertices.Count - 1);

					if (!flags[current].HasFlag(OutlineFlags.OnCurve))
						innerIndices.Add(vertices.Count - 1);
				}
			}

			start = end + 1;
			outerIndices.Add(-1); // contour end
			innerIndices.Add(-1); // contour end
		}

		Triangulate(vertices, outerIndices, innerIndices);
		return new(vertices.ToArray(), outerIndices.ToArray(), innerIndices.ToArray(), (float)spacing);
	}

	private static Glyph ReadGlyph(Stream reader, int index, Span<uint> locations, Span<int> advanceWidths) 
	{
		if (index < 0 || index >= locations.Length)
			throw new ArgumentOutOfRangeException();

		uint offset = locations[index];
		uint nextOffset = (index + 1 < locations.Length) ? locations[index + 1] : (uint)reader.Length;

		if (offset == nextOffset)
			return new([], [], [], 0);

		reader.Position = (long)offset;
		Glyph g;

		switch ((short)reader.ReadUInt16BigEndian()) 
		{
			case 0:
				throw new FormatException($"Glyph {index} has no contours.");
			case > 0:
				reader.Position = (long)offset;
				g = ReadSimpleGlyph(reader, advanceWidths[index]);
				break;
			case < 0:
				reader.Position = (long)offset;
				g = ReadCompoundGlyph(reader, advanceWidths[index]);
				break;
		}

		return g;
	}

	private static Glyph ReadSimpleGlyph(Stream reader, int spacing) 
	{
		int contoursCount = (int)(short)reader.ReadUInt16BigEndian();
		short xMin = (short)reader.ReadUInt16BigEndian();
		short yMin = (short)reader.ReadUInt16BigEndian();
		short xMax = (short)reader.ReadUInt16BigEndian();
		short yMax = (short)reader.ReadUInt16BigEndian();

		Span<int> contourEndIndices = stackalloc int[contoursCount];
		int pointsCount = 0;

		for (int i = 0; i < contourEndIndices.Length; i++) 
		{
			contourEndIndices[i] = (int)reader.ReadUInt16BigEndian();
			pointsCount = Math.Max(pointsCount, contourEndIndices[i] + 1);
		}

		reader.Position += (long)reader.ReadUInt16BigEndian() + 2;

		Span<OutlineFlags> flags = stackalloc OutlineFlags[pointsCount];

		for (int i = 0; i < flags.Length; i++) 
		{
			OutlineFlags x = (OutlineFlags)reader.ReadUInt8();
			flags[i] = x;

			if (x.HasFlag(OutlineFlags.Repeat))
				for (byte count = reader.ReadUInt8(); count > 0; count--)
					flags[++i] = x;
		}

		Span<int> xcoords = stackalloc int[pointsCount];
		Span<int> ycoords = stackalloc int[pointsCount];

		readCoords(reader, flags, sizeFlag: OutlineFlags.XSize, signOrSkipFlag: OutlineFlags.XSignOrSkip, xcoords);
		readCoords(reader, flags, sizeFlag: OutlineFlags.YSize, signOrSkipFlag: OutlineFlags.YSignOrSkip, ycoords);

		return CreateGlyph(xcoords, ycoords, flags, contourEndIndices, spacing);

		static void readCoords(Stream reader, Span<OutlineFlags> flags, OutlineFlags sizeFlag, OutlineFlags signOrSkipFlag, Span<int> coordinates) 
		{
			int value = 0;

			for (int i = 0; i < coordinates.Length; i++) 
			{
				if (flags[i].HasFlag(sizeFlag))
					value += flags[i].HasFlag(signOrSkipFlag) ? reader.ReadUInt8() : -reader.ReadUInt8();
				else if (!flags[i].HasFlag(signOrSkipFlag))
					value += (int)(short)reader.ReadUInt16BigEndian();

				coordinates[i] = value;
			}
		}
	}

	private static Glyph ReadCompoundGlyph(Stream reader, int spacing) 
	{
		return new([], [], [], 0);
	}

	private static Dictionary<uint, int> ReadCharacterMap(Stream reader) 
	{
		long tableOffset = reader.Position;

		reader.Position += 2;
		uint subtablesCount = reader.ReadUInt16BigEndian();
		uint subtableOffset = uint.MaxValue;

		for (uint i = 0; i < subtablesCount; i++) 
		{
			uint platformId = reader.ReadUInt16BigEndian();
			uint platformSpecificId = reader.ReadUInt16BigEndian();
			uint offset = reader.ReadUInt32BigEndian();

			if (platformId == 0) 
			{
				if (platformSpecificId == 4)
					subtableOffset = offset;

				if (platformSpecificId == 3 && offset == uint.MaxValue)
					subtableOffset = offset;
			}
		}

		if (subtableOffset == 0 || subtableOffset == uint.MaxValue)
			throw new NotImplementedException("Font does not contain supported character map type.");

		reader.Position = tableOffset + (long)(ulong)subtableOffset;
		int format = (int)reader.ReadUInt16BigEndian();

		var map = new Dictionary<uint, int>();

		switch (format) 
		{
			case 12:
				reader.Position += 2 + 4 + 4;
				uint groupCount = reader.ReadUInt32BigEndian();

				for (uint i = 0; i < groupCount; i++) 
				{
					uint startCharCode = reader.ReadUInt32BigEndian();
					uint endCharCode = reader.ReadUInt32BigEndian();
					uint startGlyphIndex = reader.ReadUInt32BigEndian();

					uint charCount = endCharCode - startCharCode + 1;

					for (uint charCodeOffset = 0; charCodeOffset < charCount; charCodeOffset++) 
					{
						uint charCode = startCharCode + charCodeOffset;
						uint glyphIndex = startGlyphIndex + charCodeOffset;

						map.Add(charCode, checked((int)glyphIndex));
					}
				}
				break;
			default:
				throw new NotImplementedException($"Font does not contain supported character map format - '{format}'.");
		}

		return map;
	}

	private static Table FindTable(Span<Table> tables, Tag tag) 
	{
		foreach (var x in tables)
			if (x.Tag == tag)
				return x;

		throw new FormatException($"Cannot find '{tag}' table.");
	}

	public Font(string filename) 
	{
		if (filename == null)
			throw new ArgumentNullException();

		using var reader = File.OpenRead(
			!File.Exists(filename) ? Search(filename) ?? throw new FileNotFoundException() : filename
		);

		reader.Position += 4;
		Span<Table> tables = stackalloc Table[(int)reader.ReadUInt16BigEndian()];
		reader.Position += 2 + 2 + 2;

		for (int i = 0; i < tables.Length; i++)
			tables[i] = ReadTable(reader);

		int unitsPerEm, glyphOffsetSize, glyphCount, advanceWidthsCount;

		{
			Table head = FindTable(tables, Tag.head);
			reader.Position = (long)head.Offset;

			reader.Position += 4 + 4 + 4 + 4 + 2;
			unitsPerEm = (int)reader.ReadUInt16BigEndian();
			reader.Position += 8 + 8 + 2 + 2 + 2 + 2 + 2 + 2 + 2;
			glyphOffsetSize = (reader.ReadUInt16BigEndian() == 0) ? 2 : 4;
		}

		{
			Table maxp = FindTable(tables, Tag.maxp);
			reader.Position = (long)maxp.Offset;

			reader.Position += 4;
			glyphCount = (int)reader.ReadUInt16BigEndian();
		}

		{
			Table hhea = FindTable(tables, Tag.hhea);
			reader.Position = (long)hhea.Offset;

			reader.Position += 4 + 2 + 2 + 2 + 2 + 2 + 2 + 2 + 2 + 2 + 2 + 2 + 2 + 2 + 2 + 2;
			advanceWidthsCount = (int)reader.ReadUInt16BigEndian();
		}

		Span<int> advanceWidths = stackalloc int[glyphCount];

		{
			Table hmtx = FindTable(tables, Tag.hmtx);
			reader.Position = (long)hmtx.Offset;

			for (int i = 0; i < advanceWidthsCount; i++) 
			{
				advanceWidths[i] = reader.ReadUInt16BigEndian();
				reader.Position += 2;
			}

			for (int i = advanceWidthsCount; i < glyphCount; i++)
				advanceWidths[i] = advanceWidths[advanceWidthsCount - 1];
		}

		Span<uint> glyphLocations = stackalloc uint[glyphCount + 1];

		{
			Table loca = FindTable(tables, Tag.loca);
			Table glyf = FindTable(tables, Tag.glyf);

			for (int i = 0; i < glyphLocations.Length; i++) 
			{
				reader.Position = (long)(loca.Offset + (i * glyphOffsetSize));
				glyphLocations[i] = glyf.Offset + ((glyphOffsetSize == 2) ? (uint)reader.ReadUInt16BigEndian() * 2 : reader.ReadUInt32BigEndian());
			}
		}

		{
			Table cmap = FindTable(tables, Tag.cmap);
			reader.Position = (long)cmap.Offset;

			this.map = ReadCharacterMap(reader);
		}

		this.glyphs = new Glyph[glyphCount];

		for (int i = 0; i < glyphCount; i++)
			this.glyphs[i] = ReadGlyph(reader, i, glyphLocations, advanceWidths);

		this.scale = 1f / (float)unitsPerEm;
	}

	private record struct Table(Tag Tag, uint CheckSum, uint Offset, uint Length);
	private record Glyph(Vector2[] Vertices, int[] OuterIndices, int[] InnerIndices, float Spacing);

	private enum Tag : uint
	{
		GDEF = 0x47444546,
		GPOS = 0x47504f53,
		GSUB = 0x47535542,
		HVAR = 0x48564152,
		MVAR = 0x4d564152,
		STAT = 0x53544154,
		acnt = 0x61636e74,
		ankr = 0x616e6b72,
		avar = 0x61766172,
		bdat = 0x62646174,
		bhed = 0x62686564,
		bloc = 0x626c6f63,
		bsln = 0x62736c6e,
		cmap = 0x636d6170,
		cvar = 0x63766172,
		cvt = 0x637674,
		EBSC = 0x45425343,
		fdsc = 0x66647363,
		feat = 0x66656174,
		fmtx = 0x666d7478,
		fond = 0x666f6e64,
		fpgm = 0x6670676d,
		fvar = 0x66766172,
		gasp = 0x67617370,
		glyf = 0x676c7966,
		gvar = 0x67766172,
		hdmx = 0x68646d78,
		head = 0x68656164,
		hhea = 0x68686561,
		hmtx = 0x686d7478,
		hvgl = 0x6876676c,
		hvpm = 0x6876706d,
		just = 0x6a757374,
		kern = 0x6b65726e,
		kerx = 0x6b657278,
		lcar = 0x6c636172,
		loca = 0x6c6f6361,
		ltag = 0x6c746167,
		maxp = 0x6d617870,
		meta = 0x6d657461,
		mort = 0x6d6f7274,
		morx = 0x6d6f7278,
		name = 0x6e616d65,
		opbd = 0x6f706264,
		OS2 = 0x4f532f32,
		post = 0x706f7374,
		prep = 0x70726570,
		prop = 0x70726f70,
		sbix = 0x73626978,
		trak = 0x7472616b,
		vhea = 0x76686561,
		vmtx = 0x766d7478,
		xref = 0x78726566,
		Zapf = 0x5a617066,
	}

	[Flags]
	private enum OutlineFlags : byte
	{
		OnCurve = 1 << 0,
		XSize = 1 << 1,
		YSize = 1 << 2,
		Repeat = 1 << 3,
		XSignOrSkip = 1 << 4,
		YSignOrSkip = 1 << 5,
	}
}

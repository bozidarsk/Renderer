using System;
using System.Collections.Generic;
using System.Linq;

using Vulkan;

namespace Renderer;

public static class EarClipping 
{
	public static IEnumerable<int> Triangulate(IReadOnlyList<Vector2> vertices, IList<int> indices, IEnumerable<IList<int>>? holes, bool isClockwise) 
	{
		if (vertices == null || indices == null)
			throw new ArgumentNullException();

		if (holes == null || !holes.Any())
			return Triangulate(vertices, indices, isClockwise);

		// Build a working list of polygon indices
		var merged = new List<int>(indices);

		foreach (var hole in holes) 
		{
			if (hole == null || hole.Count < 3)
				continue;

			// Skip if hole is totally outside (naive check: pick one point and test inside polygon)
			if (!PointInPolygon(vertices[hole[0]], vertices, indices))
				continue;

			// Find a bridge between outer polygon and hole
			CreateBridge(vertices, merged, hole);
		}

		// clean collinear points before ear clipping
		RemoveCollinear(vertices, merged);

		return Triangulate(vertices, merged, isClockwise);
	}

	private static bool PointInPolygon(Vector2 point, IReadOnlyList<Vector2> vertices, IList<int> indices) 
	{
		bool inside = false;

		for (int i = 0, j = indices.Count - 1; i < indices.Count; j = i++) 
		{
			Vector2 vi = vertices[indices[i]];
			Vector2 vj = vertices[indices[j]];
			if (((vi.y > point.y) != (vj.y > point.y)) &&
				(point.x < (vj.x - vi.x) * (point.y - vi.y) / (vj.y - vi.y) + vi.x))
			{
				inside = !inside;
			}
		}

		return inside;
	}

	private static void CreateBridge(IReadOnlyList<Vector2> vertices, List<int> polygon, IList<int> hole) 
	{
		// --- Ensure hole winding is opposite to polygon ---
		if (IsClockwise(vertices, polygon) == IsClockwise(vertices, hole)) 
		{
			// reverse hole order (use a local copy so we don't mutate caller)
			hole = hole.Reverse().ToList();
		}

		// 1. Pick rightmost vertex of the hole
		int holeIndex = 0;
		for (int i = 1; i < hole.Count; i++) 
		{
			if (vertices[hole[i]].x > vertices[hole[holeIndex]].x)
				holeIndex = i;
		}

		Vector2 holePoint = vertices[hole[holeIndex]];

		// 2. Find a visible polygon vertex to connect to
		int bridgeIndex = -1;
		float minDist = float.MaxValue;

		for (int i = 0; i < polygon.Count; i++) 
		{
			Vector2 p = vertices[polygon[i]];
			if (p.x <= holePoint.x) continue; // heuristic: prefer right side

			if (!Visible(holePoint, p, vertices, polygon, hole))
				continue;

			float d = Vector2.DistanceSquared(holePoint, p);
			if (d < minDist) 
			{
				minDist = d;
				bridgeIndex = i;
			}
		}

		// fallback: pick closest visible vertex
		if (bridgeIndex == -1) 
		{
			for (int i = 0; i < polygon.Count; i++) 
			{
				Vector2 p = vertices[polygon[i]];
				if (!Visible(holePoint, p, vertices, polygon, hole))
					continue;

				float d = Vector2.DistanceSquared(holePoint, p);
				if (d < minDist) 
				{
					minDist = d;
					bridgeIndex = i;
				}
			}
		}

		if (bridgeIndex == -1)
			throw new InvalidOperationException("Could not find visible bridge for hole.");

		// --- Stitch with duplication of bridge endpoints ---
		// We will construct:
		// [ polygon[0..bridgeIndex] ,
		//   hole[holeIndex .. holeEnd], hole[0 .. holeIndex-1], hole[holeIndex] (duplicate) ,
		//   polygon[bridgeIndex .. end] ]
		// This duplicates both the polygon bridge vertex (polygon[bridgeIndex]) and
		// the hole bridge vertex (hole[holeIndex]) so both sides of the bridge exist.

		var newIndices = new List<int>(polygon.Count + hole.Count + 2);

		// copy polygon up to and including the bridge vertex
		for (int i = 0; i <= bridgeIndex; i++)
			newIndices.Add(polygon[i]);

		// add hole full cycle starting at holeIndex (each hole vertex once)
		for (int i = 0; i < hole.Count; i++)
			newIndices.Add(hole[(holeIndex + i) % hole.Count]);

		// duplicate the hole bridge vertex to close the hole side of the bridge
		newIndices.Add(hole[holeIndex]);

		// continue polygon from the bridge vertex again (duplicate polygon bridge vertex here as start)
		for (int i = bridgeIndex; i < polygon.Count; i++)
			newIndices.Add(polygon[i]);

		// replace polygon with new stitched polygon
		polygon.Clear();
		polygon.AddRange(newIndices);
	}

	/// <summary>
	/// Checks if segment (a,b) is visible (does not cross polygon/hole edges).
	/// </summary>
	private static bool Visible(Vector2 a, Vector2 b, IReadOnlyList<Vector2> verts, IList<int> poly, IList<int> hole) 
	{
		// check against polygon edges
		if (!SegmentClear(a, b, verts, poly))
			return false;

		// check against hole edges
		if (!SegmentClear(a, b, verts, hole))
			return false;

		return true;
	}

	private static bool SegmentClear(Vector2 a, Vector2 b, IReadOnlyList<Vector2> verts, IList<int> loop) 
	{
		for (int i = 0; i < loop.Count; i++) 
		{
			int j = (i + 1) % loop.Count;
			Vector2 v1 = verts[loop[i]];
			Vector2 v2 = verts[loop[j]];

			// skip if sharing endpoint
			if ((v1 == a && v2 == b) || (v1 == b && v2 == a) ||
				v1 == a || v1 == b || v2 == a || v2 == b)
				continue;

			if (SegmentsIntersect(a, b, v1, v2))
				return false;
		}
		return true;
	}

	/// <summary>
	/// Segment intersection test (excluding collinear overlaps).
	/// </summary>
	private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2) 
	{
		float o1 = Orientation(p1, p2, q1);
		float o2 = Orientation(p1, p2, q2);
		float o3 = Orientation(q1, q2, p1);
		float o4 = Orientation(q1, q2, p2);

		if (o1 != o2 && o3 != o4) return true;
		return false;
	}

	private static int Orientation(Vector2 a, Vector2 b, Vector2 c) 
	{
		float val = (b.y - a.y) * (c.x - b.x) - (b.x - a.x) * (c.y - b.y);
		if (Math.Abs(val) < 1e-6f) return 0; // collinear
		return (val > 0) ? 1 : 2;			// 1=clockwise, 2=counterclockwise
	}

	private static bool IsClockwise(IReadOnlyList<Vector2> vertices, IList<int> loop) 
	{
		float sum = 0f;

		for (int i = 0; i < loop.Count; i++) 
		{
			Vector2 v1 = vertices[loop[i]];
			Vector2 v2 = vertices[loop[(i + 1) % loop.Count]];
			sum += (v2.x - v1.x) * (v2.y + v1.y);
		}

		return sum > 0f;
	}

	private static void RemoveCollinear(IReadOnlyList<Vector2> vertices, List<int> polygon, float epsilon = 1e-6f) 
	{
		if (polygon.Count <= 3)
			return;

		var cleaned = new List<int>(polygon.Count);

		for (int i = 0; i < polygon.Count; i++) 
		{
			int prev = polygon[(i - 1 + polygon.Count) % polygon.Count];
			int curr = polygon[i];
			int next = polygon[(i + 1) % polygon.Count];

			Vector2 a = vertices[prev];
			Vector2 b = vertices[curr];
			Vector2 c = vertices[next];

			if (!IsCollinear(a, b, c, epsilon))
				cleaned.Add(curr);
			// else skip "curr" because it lies on the line a→c
		}

		polygon.Clear();
		polygon.AddRange(cleaned);
	}

	private static bool IsCollinear(Vector2 a, Vector2 b, Vector2 c, float epsilon) 
	{
		// area of the triangle abc
		float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
		return Math.Abs(area) < epsilon;
	}

	public static IEnumerable<int> Triangulate(IReadOnlyList<Vector2> vertices, IList<int> indices, bool isClockwise) 
	{
		if (vertices == null || indices == null)
			throw new ArgumentNullException();

		if (indices.Count < 3)
			throw new ArgumentOutOfRangeException("Polygon must have at least 3 points.");

		if (indices[0] == indices[^1])
			indices.RemoveAt(indices.Count - 1);

		if (indices.Count < 3)
			throw new ArgumentOutOfRangeException("Polygon must have at least 3 points.");

		while (indices.Count > 3) 
		{
			bool found = false;

			for (int i = 0; i < indices.Count && !found; i++) 
			{
				int i0 = indices[(i + 0) % indices.Count];
				int i1 = indices[(i + 1) % indices.Count];
				int i2 = indices[(i + 2) % indices.Count];

				var v0 = vertices[i0];
				var v1 = vertices[i1];
				var v2 = vertices[i2];

				float cross = Vector2.Cross(v0 - v1, v2 - v1).z;

				if (
					anyInsideTriangle(indices.Where(x => x != i0 && x != i1 && x != i2).Select(x => vertices[x]), v0, v1, v2)
					||
					(isClockwise ? cross < 0 : cross > 0)
				) continue;

				yield return i0;
				yield return i1;
				yield return i2;

				indices.RemoveAt((i + 1) % indices.Count);
				found = true;
			}

			if (!found)
				throw new InvalidOperationException("No ear found.");
		}

		yield return indices[0];
		yield return indices[1];
		yield return indices[2];

		static bool anyInsideTriangle(IEnumerable<Vector2> points, Vector2 a, Vector2 b, Vector2 c) => points.Any(x => 
			{
				float cross1 = Vector2.Cross(b - a, x - a).z;
				float cross2 = Vector2.Cross(c - b, x - b).z;
				float cross3 = Vector2.Cross(a - c, x - c).z;

				bool hasNegative = (cross1 < 0) || (cross2 < 0) || (cross3 < 0);
				bool hasPositive = (cross1 > 0) || (cross2 > 0) || (cross3 > 0);

				return !(hasNegative && hasPositive);
			}
		);
	}
}

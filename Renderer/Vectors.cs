using System;

namespace Renderer;

public struct Vector4
{
	public float x, y, z, w;

	public float Length => MathF.Sqrt(this.LengthSquared);
	public float LengthSquared => x * x + y * y + z * z + w * w;
	public Vector4 Normalized => this / this.Length;

	public static readonly Vector4 Zero = new Vector4(0, 0, 0, 0);
	public static readonly Vector4 One = new Vector4(1, 1, 1, 1);

	public static Vector4 operator *(Vector4 a, float x) => new Vector4(a.x * x, a.y * x, a.z * x, a.w * x);
	public static Vector4 operator /(Vector4 a, float x) => new Vector4(a.x / x, a.y / x, a.z / x, a.w / x);
	public static Vector4 operator *(float x, Vector4 a) => new Vector4(a.x * x, a.y * x, a.z * x, a.w * x);
	public static Vector4 operator /(float x, Vector4 a) => new Vector4(a.x / x, a.y / x, a.z / x, a.w / x);
	public static Vector4 operator +(Vector4 a, Vector4 b) => new Vector4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
	public static Vector4 operator -(Vector4 a, Vector4 b) => new Vector4(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);

	public static bool operator ==(Vector4 a, Vector4 b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
	public static bool operator !=(Vector4 a, Vector4 b) => a.x != b.x && a.y != b.y && a.z != b.z && a.w != b.w;

	public static float Dot(Vector4 a, Vector4 b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

	public static float Distance(Vector4 a, Vector4 b) => (a - b).Length;
	public static float DistanceSquared(Vector4 a, Vector4 b) => (a - b).LengthSquared;

	public override bool Equals(object? other) => (other is Vector4 x) ? this == x : false;
	public override int GetHashCode() => HashCode.Combine(x, y, z, w);
	public override string ToString() => $"({x:f6}, {y:f6}, {z:f6}, {w:f6})";

	public Vector4(float x, float y, float z, float w) => (this.x, this.y, this.z, this.w) = (x, y, z, w);
}

public struct Vector3
{
	public float x, y, z;

	public float Length => MathF.Sqrt(this.LengthSquared);
	public float LengthSquared => x * x + y * y + z * z;
	public Vector3 Normalized => this / this.Length;

	public static readonly Vector3 Zero = new Vector3(0, 0, 0);
	public static readonly Vector3 One = new Vector3(1, 1, 1);
	public static readonly Vector3 Left = new Vector3(-1, 0, 0);
	public static readonly Vector3 Right = new Vector3(1, 0, 0);
	public static readonly Vector3 Down = new Vector3(0, -1, 0);
	public static readonly Vector3 Up = new Vector3(0, 1, 0);
	public static readonly Vector3 Forward = new Vector3(0, 0, 1);
	public static readonly Vector3 Back = new Vector3(0, 0, -1);

	public static Vector3 operator *(Vector3 a, float x) => new Vector3(a.x * x, a.y * x, a.z * x);
	public static Vector3 operator /(Vector3 a, float x) => new Vector3(a.x / x, a.y / x, a.z / x);
	public static Vector3 operator *(float x, Vector3 a) => new Vector3(a.x * x, a.y * x, a.z * x);
	public static Vector3 operator /(float x, Vector3 a) => new Vector3(a.x / x, a.y / x, a.z / x);
	public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
	public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);

	public static bool operator ==(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
	public static bool operator !=(Vector3 a, Vector3 b) => a.x != b.x && a.y != b.y && a.z != b.z;

	public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
	public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

	public static float Distance(Vector3 a, Vector3 b) => (a - b).Length;
	public static float DistanceSquared(Vector3 a, Vector3 b) => (a - b).LengthSquared;

	public override bool Equals(object? other) => (other is Vector3 x) ? this == x : false;
	public override int GetHashCode() => HashCode.Combine(x, y, z);
	public override string ToString() => $"({x:f6}, {y:f6}, {z:f6})";

	public Vector3(float x, float y, float z) => (this.x, this.y, this.z) = (x, y, z);
}

public struct Vector2
{
	public float x, y;

	public float Length => MathF.Sqrt(this.LengthSquared);
	public float LengthSquared => x * x + y * y;
	public Vector2 Normalized => this / this.Length;

	public static readonly Vector2 Zero = new Vector2(0, 0);
	public static readonly Vector2 One = new Vector2(1, 1);
	public static readonly Vector2 Left = new Vector2(-1, 0);
	public static readonly Vector2 Right = new Vector2(1, 0);
	public static readonly Vector2 Down = new Vector2(0, -1);
	public static readonly Vector2 Up = new Vector2(0, 1);

	public static Vector2 operator *(Vector2 a, float x) => new Vector2(a.x * x, a.y * x);
	public static Vector2 operator /(Vector2 a, float x) => new Vector2(a.x / x, a.y / x);
	public static Vector2 operator *(float x, Vector2 a) => new Vector2(a.x * x, a.y * x);
	public static Vector2 operator /(float x, Vector2 a) => new Vector2(a.x / x, a.y / x);
	public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
	public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);

	public static bool operator ==(Vector2 a, Vector2 b) => a.x == b.x && a.y == b.y;
	public static bool operator !=(Vector2 a, Vector2 b) => a.x != b.x && a.y != b.y;

	public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
	public static Vector3 Cross(Vector2 a, Vector2 b) => new Vector3(0, 0, a.x * b.y - a.y * b.x);

	public static float Distance(Vector2 a, Vector2 b) => (a - b).Length;
	public static float DistanceSquared(Vector2 a, Vector2 b) => (a - b).LengthSquared;

	public override bool Equals(object? other) => (other is Vector2 x) ? this == x : false;
	public override int GetHashCode() => HashCode.Combine(x, y);
	public override string ToString() => $"({x:f6}, {y:f6})";

	public Vector2(float x, float y) => (this.x, this.y) = (x, y);
}

public struct Vector4Int
{
	public int x, y, z, w;

	public int Length => (int)Math.Sqrt(this.LengthSquared);
	public int LengthSquared => x * x + y * y + z * z + w * w;
	public Vector4Int Normalized => this / this.Length;

	public static readonly Vector4Int Zero = new Vector4Int(0, 0, 0, 0);
	public static readonly Vector4Int One = new Vector4Int(1, 1, 1, 1);

	public static Vector4Int operator *(Vector4Int a, int x) => new Vector4Int(a.x * x, a.y * x, a.z * x, a.w * x);
	public static Vector4Int operator /(Vector4Int a, int x) => new Vector4Int(a.x / x, a.y / x, a.z / x, a.w / x);
	public static Vector4Int operator *(int x, Vector4Int a) => new Vector4Int(a.x * x, a.y * x, a.z * x, a.w * x);
	public static Vector4Int operator /(int x, Vector4Int a) => new Vector4Int(a.x / x, a.y / x, a.z / x, a.w / x);
	public static Vector4Int operator +(Vector4Int a, Vector4Int b) => new Vector4Int(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
	public static Vector4Int operator -(Vector4Int a, Vector4Int b) => new Vector4Int(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);

	public static bool operator ==(Vector4Int a, Vector4Int b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
	public static bool operator !=(Vector4Int a, Vector4Int b) => a.x != b.x && a.y != b.y && a.z != b.z && a.w != b.w;

	public static int Distance(Vector4Int a, Vector4Int b) => (a - b).Length;
	public static int DistanceSquared(Vector4Int a, Vector4Int b) => (a - b).LengthSquared;

	public override bool Equals(object? other) => (other is Vector4Int x) ? this == x : false;
	public override int GetHashCode() => HashCode.Combine(x, y, z, w);
	public override string ToString() => $"({x}, {y}, {z}, {w})";

	public Vector4Int(int x, int y, int z, int w) => (this.x, this.y, this.z, this.w) = (x, y, z, w);
}

public struct Vector3Int
{
	public int x, y, z;

	public int Length => (int)Math.Sqrt(this.LengthSquared);
	public int LengthSquared => x * x + y * y + z * z;
	public Vector3Int Normalized => this / this.Length;

	public static readonly Vector3Int Zero = new Vector3Int(0, 0, 0);
	public static readonly Vector3Int One = new Vector3Int(1, 1, 1);
	public static readonly Vector3Int Left = new Vector3Int(-1, 0, 0);
	public static readonly Vector3Int Right = new Vector3Int(1, 0, 0);
	public static readonly Vector3Int Down = new Vector3Int(0, -1, 0);
	public static readonly Vector3Int Up = new Vector3Int(0, 1, 0);
	public static readonly Vector3Int Forward = new Vector3Int(0, 0, -1);
	public static readonly Vector3Int Back = new Vector3Int(0, 0, 1);

	public static Vector3Int operator *(Vector3Int a, int x) => new Vector3Int(a.x * x, a.y * x, a.z * x);
	public static Vector3Int operator /(Vector3Int a, int x) => new Vector3Int(a.x / x, a.y / x, a.z / x);
	public static Vector3Int operator *(int x, Vector3Int a) => new Vector3Int(a.x * x, a.y * x, a.z * x);
	public static Vector3Int operator /(int x, Vector3Int a) => new Vector3Int(a.x / x, a.y / x, a.z / x);
	public static Vector3Int operator +(Vector3Int a, Vector3Int b) => new Vector3Int(a.x + b.x, a.y + b.y, a.z + b.z);
	public static Vector3Int operator -(Vector3Int a, Vector3Int b) => new Vector3Int(a.x - b.x, a.y - b.y, a.z - b.z);

	public static bool operator ==(Vector3Int a, Vector3Int b) => a.x == b.x && a.y == b.y && a.z == b.z;
	public static bool operator !=(Vector3Int a, Vector3Int b) => a.x != b.x && a.y != b.y && a.z != b.z;

	public static int Dot(Vector3Int a, Vector3Int b) => a.x * b.x + a.y * b.y + a.z * b.z;
	public static Vector3Int Cross(Vector3Int a, Vector3Int b) => new Vector3Int(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

	public static int Distance(Vector3Int a, Vector3Int b) => (a - b).Length;
	public static int DistanceSquared(Vector3Int a, Vector3Int b) => (a - b).LengthSquared;

	public override bool Equals(object? other) => (other is Vector3Int x) ? this == x : false;
	public override int GetHashCode() => HashCode.Combine(x, y, z);
	public override string ToString() => $"({x}, {y}, {z})";

	public Vector3Int(int x, int y, int z) => (this.x, this.y, this.z) = (x, y, z);
}

public struct Vector2Int
{
	public int x, y;

	public int Length => (int)Math.Sqrt(this.LengthSquared);
	public int LengthSquared => x * x + y * y;
	public Vector2Int Normalized => this / this.Length;

	public static readonly Vector2Int Zero = new Vector2Int(0, 0);
	public static readonly Vector2Int One = new Vector2Int(1, 1);
	public static readonly Vector2Int Left = new Vector2Int(-1, 0);
	public static readonly Vector2Int Right = new Vector2Int(1, 0);
	public static readonly Vector2Int Down = new Vector2Int(0, -1);
	public static readonly Vector2Int Up = new Vector2Int(0, 1);

	public static Vector2Int operator *(Vector2Int a, int x) => new Vector2Int(a.x * x, a.y * x);
	public static Vector2Int operator /(Vector2Int a, int x) => new Vector2Int(a.x / x, a.y / x);
	public static Vector2Int operator *(int x, Vector2Int a) => new Vector2Int(a.x * x, a.y * x);
	public static Vector2Int operator /(int x, Vector2Int a) => new Vector2Int(a.x / x, a.y / x);
	public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new Vector2Int(a.x + b.x, a.y + b.y);
	public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new Vector2Int(a.x - b.x, a.y - b.y);

	public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
	public static bool operator !=(Vector2Int a, Vector2Int b) => a.x != b.x && a.y != b.y;

	public static int Dot(Vector2Int a, Vector2Int b) => a.x * b.x + a.y * b.y;
	public static Vector3Int Cross(Vector2Int a, Vector2Int b) => new Vector3Int(0, 0, a.x * b.y - a.y * b.x);

	public static int Distance(Vector2Int a, Vector2Int b) => (a - b).Length;
	public static int DistanceSquared(Vector2Int a, Vector2Int b) => (a - b).LengthSquared;

	public override bool Equals(object? other) => (other is Vector2Int x) ? this == x : false;
	public override int GetHashCode() => HashCode.Combine(x, y);
	public override string ToString() => $"({x}, {y})";

	public Vector2Int(int x, int y) => (this.x, this.y) = (x, y);
}

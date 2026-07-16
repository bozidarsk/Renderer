using System;
using System.Runtime.InteropServices;

namespace Renderer;

[StructLayout(LayoutKind.Sequential)]
public struct Quaternion
{
	public float x, y, z, w; // xi + yj + zk + w

	public readonly Quaternion Inversed => new Quaternion(-x, -y, -z, w);

	public static readonly Quaternion Identity = new Quaternion(0, 0, 0, 1);

	public static Quaternion operator *(Quaternion l, Quaternion r) => new Quaternion(
		l.w * r.x + l.x * r.w + l.y * r.z - l.z * r.y,
		l.w * r.y - l.x * r.z + l.y * r.w + l.z * r.x,
		l.w * r.z + l.x * r.y - l.y * r.x + l.z * r.w,
		l.w * r.w - l.x * r.x - l.y * r.y - l.z * r.z
	);

	public override string ToString() => $"({x:f6}, {y:f6}, {z:f6}, {w:f6})";

	public static Quaternion Euler(Vector3 angles) =>
		Quaternion.AxisAngle(Vector3.Right, angles.x) * Quaternion.AxisAngle(Vector3.Up, angles.y) * Quaternion.AxisAngle(Vector3.Forward, angles.z)
	;

	public static Quaternion AxisAngle(Vector3 axis, float angle)
	{
		angle *= MathF.PI / 180f;
		angle /= 2f;

		axis = axis.Normalized * MathF.Sin(angle);

		return new Quaternion(
			axis.x,
			axis.y,
			axis.z,
			MathF.Cos(angle)
		);
	}

	private Quaternion(float x, float y, float z, float w) => (this.x, this.y, this.z, this.w) = (x, y, z, w);
}

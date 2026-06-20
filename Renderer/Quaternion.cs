using System;
using System.Runtime.InteropServices;

namespace Renderer;

[StructLayout(LayoutKind.Sequential)]
public struct Quaternion
{
	public float w, x, y, z; // w + xi + yj + zk

	public readonly Quaternion Inversed => new Quaternion(w, -x, -y, -z);

	public static readonly Quaternion Identity = new Quaternion(1, 0, 0, 0);

	public static Quaternion operator *(Quaternion l, Quaternion r) => new Quaternion(
		l.w * r.w - l.x * r.x - l.y * r.y - l.z * r.z,
		l.w * r.x + l.x * r.w + l.y * r.z - l.z * r.y,
		l.w * r.y - l.x * r.z + l.y * r.w + l.z * r.x,
		l.w * r.z + l.x * r.y - l.y * r.x + l.z * r.w
	);

	public override string ToString() => $"({w:f6}, {x:f6}, {y:f6}, {z:f6})";

	public static Quaternion Euler(Vector3 angles) =>
		Quaternion.AxisAngle(Vector3.Right, angles.x) * Quaternion.AxisAngle(Vector3.Up, angles.y) * Quaternion.AxisAngle(Vector3.Forward, angles.z)
	;

	public static Quaternion AxisAngle(Vector3 axis, float angle)
	{
		angle *= MathF.PI / 180f;
		angle /= 2f;

		axis = axis.Normalized * MathF.Sin(angle);

		return new Quaternion(
			MathF.Cos(angle),
			axis.x,
			axis.y,
			axis.z
		);
	}

	public Quaternion(float w, float x, float y, float z) => (this.w, this.x, this.y, this.z) = (w, x, y, z);
}

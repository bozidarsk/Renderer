using System;
using System.Runtime.InteropServices;

namespace Renderer;

[StructLayout(LayoutKind.Sequential)]
public struct Matrix4x4
{
	private float m00, m01, m02, m03;
	private float m10, m11, m12, m13;
	private float m20, m21, m22, m23;
	private float m30, m31, m32, m33;

	public Vector3 x
	{
		set => (m00, m10, m20) = (value.x, value.y, value.z);
		get => new(m00, m10, m20);
	}

	public Vector3 y
	{
		set => (m01, m11, m21) = (value.x, value.y, value.z);
		get => new(m01, m11, m21);
	}

	public Vector3 z
	{
		set => (m02, m12, m22) = (value.x, value.y, value.z);
		get => new(m02, m12, m22);
	}

	public Vector3 t
	{
		set => (m03, m13, m23) = (value.x, value.y, value.z);
		get => new(m03, m13, m23);
	}

	public float Determinant => 0
		+ m00 * (m11 * (m22 * m33 - m32 * m23) - m21 * (m12 * m33 - m32 * m13) + m31 * (m12 * m23 - m22 * m13))
		- m01 * (m10 * (m22 * m33 - m32 * m23) - m20 * (m12 * m33 - m32 * m13) + m30 * (m12 * m23 - m22 * m13))
		+ m02 * (m10 * (m21 * m33 - m31 * m23) - m20 * (m11 * m33 - m31 * m13) + m30 * (m11 * m23 - m21 * m13))
		- m03 * (m10 * (m21 * m32 - m31 * m22) - m20 * (m11 * m32 - m31 * m12) + m30 * (m11 * m22 - m21 * m12))
	;

	public Matrix4x4 Transposed => new()
	{
		m00 = m00,
		m01 = m10,
		m02 = m20,
		m03 = m30,

		m10 = m01,
		m11 = m11,
		m12 = m21,
		m13 = m31,

		m20 = m02,
		m21 = m12,
		m22 = m22,
		m23 = m32,

		m30 = m03,
		m31 = m13,
		m32 = m23,
		m33 = m33,
	};

	public Matrix4x4 Inversed
	{
		get
		{
			float det = this.Determinant;

			if (det > -1e-6f && det < 1e-6f)
				throw new DivideByZeroException();

			var c0 = new Vector4(
				m11 * m22 * m33 + m12 * m23 * m31 + m13 * m21 * m32 - m13 * m22 * m31 - m12 * m21 * m33 - m11 * m23 * m32,
				-m10 * m22 * m33 - m12 * m23 * m30 - m13 * m20 * m32 + m13 * m22 * m30 + m12 * m20 * m33 + m10 * m23 * m32,
				m10 * m21 * m33 + m11 * m23 * m30 + m13 * m20 * m31 - m13 * m21 * m30 - m11 * m20 * m33 - m10 * m23 * m31,
				-m10 * m21 * m32 - m11 * m22 * m30 - m12 * m20 * m31 + m12 * m21 * m30 + m11 * m20 * m32 + m10 * m22 * m31
			) / det;

			var c1 = new Vector4(
				-m01 * m22 * m33 - m02 * m23 * m31 - m03 * m21 * m32 + m03 * m22 * m31 + m02 * m21 * m33 + m01 * m23 * m32,
				m00 * m22 * m33 + m02 * m23 * m30 + m03 * m20 * m32 - m03 * m22 * m30 - m02 * m20 * m33 - m00 * m23 * m32,
				-m00 * m21 * m33 - m01 * m23 * m30 - m03 * m20 * m31 + m03 * m21 * m30 + m01 * m20 * m33 + m00 * m23 * m31,
				m00 * m21 * m32 + m01 * m22 * m30 + m02 * m20 * m31 - m02 * m21 * m30 - m01 * m20 * m32 - m00 * m22 * m31
			) / det;

			var c2 = new Vector4(
				m01 * m12 * m33 + m02 * m13 * m31 + m03 * m11 * m32 - m03 * m12 * m31 - m02 * m11 * m33 - m01 * m13 * m32,
				-m00 * m12 * m33 - m02 * m13 * m30 - m03 * m10 * m32 + m03 * m12 * m30 + m02 * m10 * m33 + m00 * m13 * m32,
				m00 * m11 * m33 + m01 * m13 * m30 + m03 * m10 * m31 - m03 * m11 * m30 - m01 * m10 * m33 - m00 * m13 * m31,
				-m00 * m11 * m32 - m01 * m12 * m30 - m02 * m10 * m31 + m02 * m11 * m30 + m01 * m10 * m32 + m00 * m12 * m31
			) / det;

			var c3 = new Vector4(
				-m01 * m12 * m23 - m02 * m13 * m21 - m03 * m11 * m22 + m03 * m12 * m21 + m02 * m11 * m23 + m01 * m13 * m22,
				m00 * m12 * m23 + m02 * m13 * m20 + m03 * m10 * m22 - m03 * m12 * m20 - m02 * m10 * m23 - m00 * m13 * m22,
				-m00 * m11 * m23 - m01 * m13 * m20 - m03 * m10 * m21 + m03 * m11 * m20 + m01 * m10 * m23 + m00 * m13 * m21,
				m00 * m11 * m22 + m01 * m12 * m20 + m02 * m10 * m21 - m02 * m11 * m20 - m01 * m10 * m22 - m00 * m12 * m21
			) / det;

			return new()
			{
				m00 = c0.x,
				m01 = c1.x,
				m02 = c2.x,
				m03 = c3.x,

				m10 = c0.y,
				m11 = c1.y,
				m12 = c2.y,
				m13 = c3.y,

				m20 = c0.z,
				m21 = c1.z,
				m22 = c2.z,
				m23 = c3.z,

				m30 = c0.w,
				m31 = c1.w,
				m32 = c2.w,
				m33 = c3.w,
			};
		}
	}

	public static readonly Matrix4x4 Identity = new()
	{
		m00 = 1,
		m01 = 0,
		m02 = 0,
		m03 = 0,

		m10 = 0,
		m11 = 1,
		m12 = 0,
		m13 = 0,

		m20 = 0,
		m21 = 0,
		m22 = 1,
		m23 = 0,

		m30 = 0,
		m31 = 0,
		m32 = 0,
		m33 = 1,
	};

	public static readonly Matrix4x4 Zero = new()
	{
		m00 = 0,
		m01 = 0,
		m02 = 0,
		m03 = 0,

		m10 = 0,
		m11 = 0,
		m12 = 0,
		m13 = 0,

		m20 = 0,
		m21 = 0,
		m22 = 0,
		m23 = 0,

		m30 = 0,
		m31 = 0,
		m32 = 0,
		m33 = 0,
	};

	public static Matrix4x4 operator *(Matrix4x4 left, Matrix4x4 right) => new()
	{
		m00 = left.m00 * right.m00 + left.m01 * right.m10 + left.m02 * right.m20 + left.m03 * right.m30,
		m01 = left.m00 * right.m01 + left.m01 * right.m11 + left.m02 * right.m21 + left.m03 * right.m31,
		m02 = left.m00 * right.m02 + left.m01 * right.m12 + left.m02 * right.m22 + left.m03 * right.m32,
		m03 = left.m00 * right.m03 + left.m01 * right.m13 + left.m02 * right.m23 + left.m03 * right.m33,

		m10 = left.m10 * right.m00 + left.m11 * right.m10 + left.m12 * right.m20 + left.m13 * right.m30,
		m11 = left.m10 * right.m01 + left.m11 * right.m11 + left.m12 * right.m21 + left.m13 * right.m31,
		m12 = left.m10 * right.m02 + left.m11 * right.m12 + left.m12 * right.m22 + left.m13 * right.m32,
		m13 = left.m10 * right.m03 + left.m11 * right.m13 + left.m12 * right.m23 + left.m13 * right.m33,

		m20 = left.m20 * right.m00 + left.m21 * right.m10 + left.m22 * right.m20 + left.m23 * right.m30,
		m21 = left.m20 * right.m01 + left.m21 * right.m11 + left.m22 * right.m21 + left.m23 * right.m31,
		m22 = left.m20 * right.m02 + left.m21 * right.m12 + left.m22 * right.m22 + left.m23 * right.m32,
		m23 = left.m20 * right.m03 + left.m21 * right.m13 + left.m22 * right.m23 + left.m23 * right.m33,

		m30 = left.m30 * right.m00 + left.m31 * right.m10 + left.m32 * right.m20 + left.m33 * right.m30,
		m31 = left.m30 * right.m01 + left.m31 * right.m11 + left.m32 * right.m21 + left.m33 * right.m31,
		m32 = left.m30 * right.m02 + left.m31 * right.m12 + left.m32 * right.m22 + left.m33 * right.m32,
		m33 = left.m30 * right.m03 + left.m31 * right.m13 + left.m32 * right.m23 + left.m33 * right.m33,
	};

	public static Vector4 operator *(Matrix4x4 m, Vector4 v) => new()
	{
		x = m.m00 * v.x + m.m01 * v.y + m.m02 * v.z + m.m03 * v.w,
		y = m.m10 * v.x + m.m11 * v.y + m.m12 * v.z + m.m13 * v.w,
		z = m.m20 * v.x + m.m21 * v.y + m.m22 * v.z + m.m23 * v.w,
		w = m.m30 * v.x + m.m31 * v.y + m.m32 * v.z + m.m33 * v.w,
	};

	public override string ToString() =>
	$$$"""
	{
		{{{m00:f6}}} {{{m01:f6}}} {{{m02:f6}}} {{{m03:f6}}}
		{{{m10:f6}}} {{{m11:f6}}} {{{m12:f6}}} {{{m13:f6}}}
		{{{m20:f6}}} {{{m21:f6}}} {{{m22:f6}}} {{{m23:f6}}}
		{{{m30:f6}}} {{{m31:f6}}} {{{m32:f6}}} {{{m33:f6}}}
	}
	""";

	public static Matrix4x4 Perspective(float fov, float ratio, float near, float far)
	{
		float f = 1f / MathF.Tan((fov * MathF.PI / 180f) / 2f);

		return Matrix4x4.Zero with
		{
			m00 = f / ratio,
			m11 = f,
			m22 = -near / (far - near),
			m23 = (near * far) / (far - near),
			m32 = 1f,
		};
	}

	public static Matrix4x4 Orthographic(float left, float right, float bottom, float top, float near, float far)
	{
		return Matrix4x4.Identity with
		{
			m00 = 2f / (right - left),
			m11 = 2f / (top - bottom),
			m22 = -1f / (far - near),
			m03 = -(right + left) / (right - left),
			m13 = (top + bottom) / (top - bottom),
			m23 = far / (far - near)
		};
	}

	public static Matrix4x4 LookAt(Vector3 from, Vector3 to, Vector3 up)
	{
		Vector3 forward = (to - from).Normalized;
		Vector3 right = Vector3.Cross(forward, up).Normalized;
		Vector3 cameraUp = Vector3.Cross(right, forward);

		return new()
		{
			m00 = right.x,
			m01 = cameraUp.x,
			m02 = forward.x,
			m03 = 0,
			m10 = right.y,
			m11 = cameraUp.y,
			m12 = forward.y,
			m13 = 0,
			m20 = right.z,
			m21 = cameraUp.z,
			m22 = forward.z,
			m23 = 0,
			m30 = -Vector3.Dot(right, from),
			m31 = -Vector3.Dot(cameraUp, from),
			m32 = -Vector3.Dot(forward, from),
			m33 = 1,
		};
	}

	public static Matrix4x4 Translate(Vector3 v) => Matrix4x4.Identity with { m03 = v.x, m13 = v.y, m23 = v.z };
	public static Matrix4x4 Scale(Vector3 v) => Matrix4x4.Identity with { m00 = v.x, m11 = v.y, m22 = v.z };
	public static Matrix4x4 Rotate(Quaternion q)
	{
		// from https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Math/Matrix4x4.cs#L370

		// Precalculate coordinate products
		float x = q.x * 2;
		float y = q.y * 2;
		float z = q.z * 2;
		float xx = q.x * x;
		float yy = q.y * y;
		float zz = q.z * z;
		float xy = q.x * y;
		float xz = q.x * z;
		float yz = q.y * z;
		float wx = q.w * x;
		float wy = q.w * y;
		float wz = q.w * z;

		// Calculate 3x3 matrix from orthonormal basis
		Matrix4x4 m;
		m.m00 = 1 - (yy + zz); m.m01 = xy + wz; m.m02 = xz - wy; m.m03 = 0;
		m.m10 = xy - wz; m.m11 = 1 - (xx + zz); m.m12 = yz + wx; m.m13 = 0;
		m.m20 = xz + wy; m.m21 = yz - wx; m.m22 = 1 - (xx + yy); m.m23 = 0;
		m.m30 = 0; m.m31 = 0; m.m32 = 0; m.m33 = 1;
		return m;
	}

	public static Matrix4x4 TRS(Vector3 t, Quaternion r, Vector3 s) => Translate(t) * (Rotate(r) * Scale(s));
}

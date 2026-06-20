using System;
using System.Runtime.InteropServices;

namespace Renderer;

[StructLayout(LayoutKind.Sequential)]
public struct Matrix4x4
{
	public float xx, yx, zx, tx;
	public float xy, yy, zy, ty;
	public float xz, yz, zz, tz;
	public float xw, yw, zw, tw;

	public Vector4 x => new Vector4(xx, xy, xz, xw);
	public Vector4 y => new Vector4(yx, yy, yz, yw);
	public Vector4 z => new Vector4(zx, zy, zz, zw);
	public Vector4 t => new Vector4(tx, ty, tz, tw);

	public float Determinant => 0
		+ xx * (yy * (zz * tw - zw * tz) - yz * (zy * tw - zw * ty) + yw * (zy * tz - zz * ty))
		- yx * (xy * (zz * tw - zw * tz) - xz * (zy * tw - zw * ty) + xw * (zy * tz - zz * ty))
		+ zx * (xy * (yz * tw - yw * tz) - xz * (yy * tw - yw * ty) + xw * (yy * tz - yz * ty))
		- tx * (xy * (yz * zw - yw * zz) - xz * (yy * zw - yw * zy) + xw * (yy * zz - yz * zy))
	;

	public Matrix4x4 Transposed => new Matrix4x4(
		new Vector4(xx, yx, zx, tx),
		new Vector4(xy, yy, zy, ty),
		new Vector4(xz, yz, zz, tz),
		new Vector4(xw, yw, zw, tw)
	);

	public Matrix4x4 Inverse
	{
		get
		{
			float det = this.Determinant;

			if (det > -1e-6f && det < 1e-6f)
				throw new DivideByZeroException();

			var x = new Vector4(
				yy * zz * tw + zy * tz * yw + ty * yz * zw - ty * zz * yw - zy * yz * tw - yy * tz * zw,
				-xy * zz * tw - zy * tz * xw - ty * xz * zw + ty * zz * xw + zy * xz * tw + xy * tz * zw,
				xy * yz * tw + yy * tz * xw + ty * xz * yw - ty * yz * xw - yy * xz * tw - xy * tz * yw,
				-xy * yz * zw - yy * zz * xw - zy * xz * yw + zy * yz * xw + yy * xz * zw + xy * zz * yw
			) / det;

			var y = new Vector4(
				-yx * zz * tw - zx * tz * yw - tx * yz * zw + tx * zz * yw + zx * yz * tw + yx * tz * zw,
				xx * zz * tw + zx * tz * xw + tx * xz * zw - tx * zz * xw - zx * xz * tw - xx * tz * zw,
				-xx * yz * tw - yx * tz * xw - tx * xz * yw + tx * yz * xw + yx * xz * tw + xx * tz * yw,
				xx * yz * zw + yx * zz * xw + zx * xz * yw - zx * yz * xw - yx * xz * zw - xx * zz * yw
			) / det;

			var z = new Vector4(
				yx * zy * tw + zx * ty * yw + tx * yy * zw - tx * zy * yw - zx * yy * tw - yx * ty * zw,
				-xx * zy * tw - zx * ty * xw - tx * xy * zw + tx * zy * xw + zx * xy * tw + xx * ty * zw,
				xx * yy * tw + yx * ty * xw + tx * xy * yw - tx * yy * xw - yx * xy * tw - xx * ty * yw,
				-xx * yy * zw - yx * zy * xw - zx * xy * yw + zx * yy * xw + yx * xy * zw + xx * zy * yw
			) / det;

			var w = new Vector4(
				-yx * zy * tz - zx * ty * yz - tx * yy * zz + tx * zy * yz + zx * yy * tz + yx * ty * zz,
				xx * zy * tz + zx * ty * xz + tx * xy * zz - tx * zy * xz - zx * xy * tz - xx * ty * zz,
				-xx * yy * tz - yx * ty * xz - tx * xy * yz + tx * yy * xz + yx * xy * tz + xx * ty * yz,
				xx * yy * zz + yx * zy * xz + zx * xy * yz - zx * yy * xz - yx * xy * zz - xx * zy * yz
			) / det;

			return new(x, y, z, w);
		}
	}

	public static readonly Matrix4x4 Identity = new Matrix4x4()
	{
		xx = 1,
		yx = 0,
		zx = 0,
		tx = 0,
		xy = 0,
		yy = 1,
		zy = 0,
		ty = 0,
		xz = 0,
		yz = 0,
		zz = 1,
		tz = 0,
		xw = 0,
		yw = 0,
		zw = 0,
		tw = 1,
	};

	public static readonly Matrix4x4 Zero = new Matrix4x4()
	{
		xx = 0,
		yx = 0,
		zx = 0,
		tx = 0,
		xy = 0,
		yy = 0,
		zy = 0,
		ty = 0,
		xz = 0,
		yz = 0,
		zz = 0,
		tz = 0,
		xw = 0,
		yw = 0,
		zw = 0,
		tw = 0,
	};

	public static Matrix4x4 operator *(Matrix4x4 l, Matrix4x4 r) => new Matrix4x4(
		l * r.x,
		l * r.y,
		l * r.z,
		l * r.t
	);

	public static Vector4 operator *(Matrix4x4 m, Vector4 v) => (m.x * v.x) + (m.y * v.y) + (m.z * v.z) + (m.t * v.w);

	public override string ToString() => $"{xx:f6} {yx:f6} {zx:f6} {tx:f6}\n{xy:f6} {yy:f6} {zy:f6} {ty:f6}\n{xz:f6} {yz:f6} {zz:f6} {tz:f6}\n{xw:f6} {yw:f6} {zw:f6} {tw:f6}";

	public static Matrix4x4 Perspective(float fov, float ratio, float near, float far)
	{
		float f = 1f / MathF.Tan((fov * MathF.PI / 180f) / 2f);

		return Matrix4x4.Zero with
		{
			xx = f / ratio,
			yy = -f,
			zz = near / (far - near),
			tz = (near * far) / (far - near),
			zw = -1f
		};
	}

	public static Matrix4x4 Orthographic(float left, float right, float bottom, float top, float near, float far)
	{
		return Matrix4x4.Identity with
		{
			xx = 2f / (right - left),
			yy = -2f / (top - bottom),
			zz = -2f / (near - far),

			tx = -(right + left) / (right - left),
			ty = -(top + bottom) / (top - bottom),
			tz = near / (near - far)
		};
	}

	public static Matrix4x4 LookAt(Vector3 from, Vector3 to, Vector3 up)
	{
		Vector3 forward = (to - from).Normalized;
		Vector3 right = Vector3.Cross(forward, up).Normalized;
		Vector3 cameraUp = Vector3.Cross(right, forward);

		forward *= -1;

		return new()
		{
			xx = right.x,
			yx = cameraUp.x,
			zx = forward.x,
			tx = 0,
			xy = right.y,
			yy = cameraUp.y,
			zy = forward.y,
			ty = 0,
			xz = right.z,
			yz = cameraUp.z,
			zz = forward.z,
			tz = 0,
			xw = -Vector3.Dot(right, from),
			yw = -Vector3.Dot(cameraUp, from),
			zw = -Vector3.Dot(forward, from),
			tw = 1,
		};
	}


	public static Matrix4x4 Translate(Vector3 v) => Matrix4x4.Identity with { tx = v.x, ty = v.y, tz = v.z };
	public static Matrix4x4 Scale(Vector3 v) => Matrix4x4.Identity with { xx = v.x, yy = v.y, zz = v.z };
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
		m.xx = 1 - (yy + zz); m.yx = xy + wz; m.zx = xz - wy; m.tx = 0;
		m.xy = xy - wz; m.yy = 1 - (xx + zz); m.zy = yz + wx; m.ty = 0;
		m.xz = xz + wy; m.yz = yz - wx; m.zz = 1 - (xx + yy); m.tz = 0;
		m.xw = 0; m.yw = 0; m.zw = 0; m.tw = 1;
		return m;
	}

	public static Matrix4x4 TRS(Vector3 t, Quaternion r, Vector3 s) => Translate(t) * (Rotate(r) * Scale(s));

	public Matrix4x4(Vector4 x, Vector4 y, Vector4 z, Vector4 t)
	{
		this.xx = x.x;
		this.xy = x.y;
		this.xz = x.z;
		this.xw = x.w;

		this.yx = y.x;
		this.yy = y.y;
		this.yz = y.z;
		this.yw = y.w;

		this.zx = z.x;
		this.zy = z.y;
		this.zz = z.z;
		this.zw = z.w;

		this.tx = t.x;
		this.ty = t.y;
		this.tz = t.z;
		this.tw = t.w;
	}
}

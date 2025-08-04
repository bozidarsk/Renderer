using Vulkan;

namespace Renderer;

public class Transform : Component
{
	private Matrix4x4 matrix;

	public Transform Inverse => new(matrix.Inverse);

	public void Translate(Vector3 v) => matrix *= Matrix4x4.Translate(v);
	public void Scale(Vector3 v) => matrix *= Matrix4x4.Scale(v);
	public void Rotate(Quaternion q) => matrix *= Matrix4x4.Rotate(q);
	public void TRS(Vector3 t, Quaternion q, Vector3 s) => matrix *= Matrix4x4.TRS(t, q, s);

	public static implicit operator Matrix4x4 (Transform x) => x.matrix;

	public Transform() => matrix = Matrix4x4.Identity;
	public Transform(Matrix4x4 matrix) => this.matrix = matrix;
}


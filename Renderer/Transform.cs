namespace Renderer;

public class Transform : Component
{
	private Matrix4x4 matrix;

	public Transform Inversed => new(matrix.Inversed);

	public Vector3 Position
	{
		set
		{
			field = value;
			matrix.t = value;
		}
		get;
	} = Vector3.Zero;

	public Vector3 Scale
	{
		set
		{
			field = value;
			matrix.x = matrix.x.Normalized * value.x;
			matrix.y = matrix.y.Normalized * value.y;
			matrix.z = matrix.z.Normalized * value.z;
		}
		get;
	} = Vector3.One;

	public Quaternion Rotation
	{
		set
		{
			field = value;

			var rotation = Matrix4x4.Rotate(value);
			var scale = this.Scale;

			var x = matrix.x;
			var rotx = rotation.x;
			x.x = rotx.x * scale.x;
			x.y = rotx.y * scale.y;
			x.z = rotx.z * scale.z;
			matrix.x = x;

			var y = matrix.x;
			var roty = rotation.y;
			y.x = roty.x * scale.x;
			y.y = roty.y * scale.y;
			y.z = roty.z * scale.z;
			matrix.y = y;

			var z = matrix.x;
			var rotz = rotation.z;
			z.x = rotz.x * scale.x;
			z.y = rotz.y * scale.y;
			z.z = rotz.z * scale.z;
			matrix.z = z;
		}
		get;
	} = Quaternion.Identity;

	public Vector3 Right => matrix.x.Normalized;
	public Vector3 Left => -matrix.x.Normalized;
	public Vector3 Up => matrix.y.Normalized;
	public Vector3 Down => -matrix.y.Normalized;
	public Vector3 Forward => matrix.z.Normalized;
	public Vector3 Back => -matrix.z.Normalized;

	public void Translate(Vector3 dir) => matrix *= Matrix4x4.Translate(dir);
	public void Rotate(Quaternion rot) => matrix *= Matrix4x4.Rotate(rot);

	public static explicit operator Matrix4x4(Transform x) => x.matrix;

	public Transform() => matrix = Matrix4x4.Identity;
	public Transform(Matrix4x4 matrix) => this.matrix = matrix;
}

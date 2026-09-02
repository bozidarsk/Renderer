namespace Renderer;

public struct Sides<T> where T : struct
{
	public T Top, Bottom, Left, Right;

	public override string ToString() => $"(top: {Top}, bottom: {Bottom}, left: {Left}, right: {Right})";

	public Sides(T top = default, T bottom = default, T left = default, T right = default)
	{
		this.Top = top;
		this.Bottom = bottom;
		this.Left = left;
		this.Right = right;
	}
}

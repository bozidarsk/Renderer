namespace Renderer;

public struct Rect
{
	public float x, y, Width, Height;

	public Vector2 Center => new(x + (Width / 2f), y + (Height / 2f));
	public Vector2 Position => new(x, y);
	public Vector2 Size => new(Width, Height);

	public bool Overlaps(Rect other) => (other.x > this.x && other.x < this.x + this.Width) && (other.y > this.y && other.y < this.y + this.Width);
	public bool Contains(Rect other) => Overlaps(other) && Overlaps(other with { x = other.x + other.Width, y = other.y + other.Height });

	public override string ToString() => $"(x: {x}, y: {y}, width: {Width}, height: {Height})";

	public Rect(float x, float y, float width, float height)
	{
		this.x = x;
		this.y = y;
		this.Width = width;
		this.Height = height;
	}
}

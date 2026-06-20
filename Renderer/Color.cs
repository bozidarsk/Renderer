namespace Renderer;

public struct Color
{
	public float r, g, b, a;

	public static readonly Color Transparent = new(0, 0, 0, 0);
	public static readonly Color Black = new(0, 0, 0, 1);
	public static readonly Color White = new(1, 1, 1, 1);
	public static readonly Color Red = new(1, 0, 0, 1);
	public static readonly Color Green = new(0, 1, 0, 1);
	public static readonly Color Blue = new(0, 0, 1, 1);
	public static readonly Color Yellow = new(1, 1, 0, 1);
	public static readonly Color Cyan = new(0, 1, 1, 1);
	public static readonly Color Magenta = new(1, 0, 1, 1);

	public static Color operator *(Color a, float x) => new(a.r * x, a.g * x, a.b * x, a.a * x);
	public static Color operator *(float x, Color a) => new(a.r * x, a.g * x, a.b * x, a.a * x);
	public static Color operator /(Color a, float x) => new(a.r / x, a.g / x, a.b / x, a.a / x);
	public static Color operator +(Color a, Color b) => new(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
	public static Color operator -(Color a, Color b) => new(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);

	// argb
	public static explicit operator uint(Color color)
	{
		uint result = 0;

		result <<= 8;
		result |= (uint)(color.a * 255f) & 0xff;

		result <<= 8;
		result |= (uint)(color.r * 255f) & 0xff;

		result <<= 8;
		result |= (uint)(color.g * 255f) & 0xff;

		result <<= 8;
		result |= (uint)(color.b * 255f) & 0xff;

		return result;
	}

	// argb
	public static explicit operator Color(uint color)
	{
		var result = new Color();

		result.a = (float)((color >> 24) & 0xff) / 255f;
		result.r = (float)((color >> 16) & 0xff) / 255f;
		result.g = (float)((color >> 8) & 0xff) / 255f;
		result.b = (float)((color >> 0) & 0xff) / 255f;

		return result;
	}

	public override string ToString() => $"#{(uint)this:x8}";

	public Color(float r, float g, float b, float a = 1f) => (this.r, this.g, this.b, this.a) = (r, g, b, a);
}

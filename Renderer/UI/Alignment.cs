namespace Renderer.UI;

[System.Flags]
public enum Alignment
{
	None = 0,
	Top = 1 << 0,
	Bottom = 1 << 1,
	Left = 1 << 2,
	Right = 1 << 3,
	Center = Top | Bottom | Left | Right,
	TopLeft = Top | Left,
	TopRight = Top | Right,
	BottomLeft = Bottom | Left,
	BottomRight = Bottom | Right,
}

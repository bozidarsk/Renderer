using Renderer;

namespace Renderer.UI;

public class RectTransform : Transform
{
	public Rect Rect;
	public Alignment Alignment = Alignment.Center;
	public Anchors Anchors = Anchors.None;
	public Sides<float> Margin = new(0, 0, 0, 0);
}

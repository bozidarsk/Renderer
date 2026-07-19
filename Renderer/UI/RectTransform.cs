using Renderer;

namespace Renderer.UI;

public class RectTransform : Transform
{
	public Anchors Anchors { get; } = new(false, false, false, false);
}

public record Anchors(bool Top, bool Bottom, bool Left, bool Right);

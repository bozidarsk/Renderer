using System.Collections.Generic;
using System.Linq;

using Renderer;

namespace Renderer.UI;

public class Panel : Control
{
	private readonly RectTransform rectTransform;

	internal override void ComputeLayout()
	{
		var availableSpaces = new List<Rect>() { rectTransform.Rect };

		foreach (var child in Children.OfType<UIObject>())
		{
			if (!child.TryGetComponent(out RectTransform childRectTransform))
				continue;

			for (int i = 0; i < availableSpaces.Count; i++)
			{
				var space = availableSpaces[i];

				var requestedWidth = childRectTransform.Margin.Left + childRectTransform.Rect.Width + childRectTransform.Margin.Right;
				var requestedHeight = childRectTransform.Margin.Top + childRectTransform.Rect.Height + childRectTransform.Margin.Bottom;

				if (requestedWidth > space.Width || requestedHeight > space.Height)
					continue;

				if ((childRectTransform.Anchors & Anchors.Right) != 0)
					requestedWidth = space.Width;

				if ((childRectTransform.Anchors & Anchors.Bottom) != 0)
					requestedHeight = space.Height;

				childRectTransform.LocalPosition = new(
					space.x - childRectTransform.Rect.x + childRectTransform.Margin.Left,
					space.y - childRectTransform.Rect.y - childRectTransform.Margin.Top,
					childRectTransform.LocalPosition.z
				);

				if (space.Width == requestedWidth && space.Height == requestedHeight)
				{
					space.Width = 0;
					space.Height = 0;
				}
				else if (space.Width == requestedWidth)
				{
					space.y -= requestedHeight;
					space.Height -= requestedHeight;
				}
				else if (space.Height == requestedHeight)
				{
					space.x += requestedWidth;
					space.Width -= requestedWidth;
				}
				else
				{
					availableSpaces.Add(new(x: space.x, y: space.y - requestedHeight, width: space.Width, height: space.Height - requestedHeight));

					space.x += requestedWidth;
					space.Width -= requestedWidth;
					space.Height = requestedHeight;
				}

				availableSpaces[i] = space;

				if (child is Control childControl)
					childControl.ComputeLayout();

				break;
			}
		}
	}

	public Panel(Scene scene) : base(scene) => this.rectTransform = GetComponent<RectTransform>();
}

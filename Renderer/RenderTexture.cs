using System;

namespace Renderer;

public class RenderTexture
{
	public int Width { get; }
	public int Height { get; }
	public RenderPassDefinition RenderPass { get; }
	public Texture[] Images { get; }

	public RenderTexture(int width, int height, RenderPassDefinition renderPass, Texture[] images)
	{
		if (width <= 0 || height <= 0)
			throw new ArgumentOutOfRangeException();

		this.Width = width;
		this.Height = height;
		this.RenderPass = renderPass ?? throw new ArgumentNullException();
		this.Images = images ?? throw new ArgumentNullException();
	}
}

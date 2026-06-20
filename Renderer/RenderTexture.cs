using System;

using Vulkan;

namespace Renderer;

public class RenderTexture : Texture
{
	public RenderTexture(int width, int height, Format format) : base(width, height, usage: ImageUsage.ColorAttachment | ImageUsage.Sampled | ImageUsage.TransferSrc, aspect: ImageAspect.Color, format: format, type: ImageType.Generic2D)
	{
	}
}

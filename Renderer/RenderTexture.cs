using System;

using Vulkan;

namespace Renderer;

public sealed class RenderTexture : IDisposable
{
	public Framebuffer Framebuffer { get; }
	public Extent2D Extent { get; }

	public Texture Texture { get; }
	public Texture DepthTexture { get; }

	public int Width => (int)this.Extent.Width;
	public int Height => (int)this.Extent.Height;

	public Image Image => this.Texture.Image;
	public ImageView ImageView => this.Texture.ImageView;
	public DeviceMemory? ImageMemory => this.Texture.ImageMemory;
	public Sampler Sampler => this.Texture.Sampler;
	public Format ImageFormat => this.Texture.Format;

	public void Dispose() 
	{
		Framebuffer.Dispose();
		Texture.Dispose();
		DepthTexture.Dispose();
	}

	public RenderTexture(Program vk, int width, int height, Format format) : this(vk, new((uint)width, (uint)height), format) {}
	public RenderTexture(Program vk, Extent2D extent, Format format) 
	{
		this.Extent = extent;

		var depthFormat = vk.FindSupportedFormat(
			[ Format.D32SFloat, Format.D32SFloatS8UInt, Format.D24UNormS8UInt ],
			ImageTiling.Optimal,
			FormatFeatures.DepthStencilAttachment
		);

		this.Texture = new(vk, extent, ImageUsage.ColorAttachment | ImageUsage.Sampled, ImageAspect.Color, format, ImageType.Generic2D);
		this.DepthTexture = new(vk, extent, ImageUsage.DepthStencilAttachment, ImageAspect.Depth, depthFormat, ImageType.Generic2D);

		vk.TransitionImageLayout(this.Image, ImageLayout.Undefined, ImageLayout.ShaderReadOnlyOptimal);

		using var framebufferCreateInfo = new FramebufferCreateInfo(
			type: StructureType.FramebufferCreateInfo,
			next: default,
			flags: default,
			renderPass: vk.RenderPass,
			attachments: [ this.Texture.ImageView, this.DepthTexture.ImageView ],
			width: this.Extent.Width,
			height: this.Extent.Height,
			layers: 1
		);

		this.Framebuffer = framebufferCreateInfo.CreateFramebuffer(vk.Device, vk.Allocator);
	}
}

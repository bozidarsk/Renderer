using System;

using Vulkan;

namespace Renderer;

public sealed class RenderTexture : IDisposable, IInfoProvider
{
	public RenderPass RenderPass { get; }
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
	public Format DepthFormat => this.DepthTexture.Format;

	public Info Info => new RenderTextureInfo(this.Extent, this.RenderPass, this.Framebuffer, this.Image, this.ImageView, this.Sampler);

	public void Dispose() 
	{
		RenderPass.Dispose();
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

		this.Texture = new(vk, extent, ImageUsage.ColorAttachment | ImageUsage.Sampled | ImageUsage.TransferSrc, ImageAspect.Color, format, ImageType.Generic2D);
		this.DepthTexture = new(vk, extent, ImageUsage.DepthStencilAttachment, ImageAspect.Depth, depthFormat, ImageType.Generic2D);

		vk.TransitionImageLayout(this.Image, ImageLayout.Undefined, ImageLayout.ShaderReadOnlyOptimal);

		var colorAttachment = new AttachmentDescription(
			flags: default,
			format: this.ImageFormat,
			samples: SampleCount.Bit1,
			loadOp: AttachmentLoadOp.Clear,
			storeOp: AttachmentStoreOp.Store,
			stencilLoadOp: AttachmentLoadOp.DontCare,
			stencilStoreOp: AttachmentStoreOp.DontCare,
			initialLayout: ImageLayout.Undefined,
			finalLayout: ImageLayout.PresentSrc
		);

		var colorAttachmentRef = new AttachmentReference(
			attachment: 0,
			layout: ImageLayout.ColorAttachmentOptimal
		);

		var depthAttachment = new AttachmentDescription(
			flags: default,
			format: this.DepthFormat,
			samples: SampleCount.Bit1,
			loadOp: AttachmentLoadOp.Clear,
			storeOp: AttachmentStoreOp.DontCare,
			stencilLoadOp: AttachmentLoadOp.DontCare,
			stencilStoreOp: AttachmentStoreOp.DontCare,
			initialLayout: ImageLayout.Undefined,
			finalLayout: ImageLayout.DepthStencilAttachmentOptimal
		);

		var depthAttachmentRef = new AttachmentReference(
			attachment: 1,
			layout: ImageLayout.DepthStencilAttachmentOptimal
		);

		using var subpass = new SubpassDescription(
			flags: default,
			pipelineBindPoint: PipelineBindPoint.Graphics,
			inputAttachments: null,
			colorAttachments: [ colorAttachmentRef ],
			resolveAttachments: null,
			depthStencilAttachment: depthAttachmentRef,
			preserveAttachments: null
		);

		var dependency = new SubpassDependency(
			srcSubpass: ~0u,
			dstSubpass: 0,
			srcStageMask: PipelineStage.ColorAttachmentOutput | PipelineStage.EarlyFragmentTests,
			dstStageMask: PipelineStage.ColorAttachmentOutput | PipelineStage.EarlyFragmentTests,
			srcAccessMask: 0,
			dstAccessMask: Access.ColorAttachmentWrite | Access.DepthStencilAttachmentWrite,
			dependencyFlags: default
		);

		using var renderPassCreateInfo = new RenderPassCreateInfo(
			type: StructureType.RenderPassCreateInfo,
			next: default,
			flags: default,
			attachments: [ colorAttachment, depthAttachment ],
			subpasses: [ subpass ],
			dependencies: [ dependency ]
		);

		this.RenderPass = renderPassCreateInfo.CreateRenderPass(vk.Device, vk.Allocator);

		using var framebufferCreateInfo = new FramebufferCreateInfo(
			type: StructureType.FramebufferCreateInfo,
			next: default,
			flags: default,
			renderPass: this.RenderPass,
			attachments: [ this.Texture.ImageView, this.DepthTexture.ImageView ],
			width: this.Extent.Width,
			height: this.Extent.Height,
			layers: 1
		);

		this.Framebuffer = framebufferCreateInfo.CreateFramebuffer(vk.Device, vk.Allocator);
	}
}

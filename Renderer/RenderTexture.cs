using System;

using Vulkan;

namespace Renderer;

public class RenderTexture : Texture, IDisposable
{
	public RenderPass RenderPass { get; }
	public Framebuffer Framebuffer { get; }

	private Texture DepthTexture { get; }

	public Format ColorFormat => this.Format;
	public Format DepthFormat => this.DepthTexture.Format;

	public override void Dispose()
	{
		RenderPass.Dispose();
		Framebuffer.Dispose();
		DepthTexture.Dispose();

		base.Dispose();
	}

	public RenderTexture(Renderer renderer, int width, int height, Format format) : this(renderer, new((uint)width, (uint)height), format) { }

	public RenderTexture(Renderer renderer, Extent2D extent, Format format) : base(renderer, extent, ImageUsage.ColorAttachment | ImageUsage.Sampled | ImageUsage.TransferSrc, ImageAspect.Color, format, ImageType.Generic2D)
	{
		var depthFormat = renderer.FindSupportedFormat(
			[Format.D32SFloat, Format.D32SFloatS8UInt, Format.D24UNormS8UInt],
			ImageTiling.Optimal,
			FormatFeatures.DepthStencilAttachment
		);

		this.DepthTexture = new(renderer, extent, ImageUsage.DepthStencilAttachment, ImageAspect.Depth, depthFormat, ImageType.Generic2D);

		renderer.TransitionImageLayout(this.Image, ImageLayout.Undefined, ImageLayout.ShaderReadOnlyOptimal);

		var colorAttachment = new AttachmentDescription(
			flags: default,
			format: this.ColorFormat,
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
			colorAttachments: [colorAttachmentRef],
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
			next: default,
			flags: default,
			attachments: [colorAttachment, depthAttachment],
			subpasses: [subpass],
			dependencies: [dependency]
		);

		this.RenderPass = renderPassCreateInfo.CreateRenderPass(renderer.Device, renderer.Allocator);

		using var framebufferCreateInfo = new FramebufferCreateInfo(
			next: default,
			flags: default,
			renderPass: this.RenderPass,
			attachments: [this.ImageView, this.DepthTexture.ImageView],
			width: this.Extent.Width,
			height: this.Extent.Height,
			layers: 1
		);

		this.Framebuffer = framebufferCreateInfo.CreateFramebuffer(renderer.Device, renderer.Allocator);
	}
}

using System;
using System.Linq;

using Vulkan;

namespace Renderer;

public class RenderTarget : Asset
{
	public int Width { get; }
	public int Height { get; }

	public Attachment[] ColorAttachments { get; }
	public Attachment? DepthAttachment { get; }
	public Attachment? StencilAttachment { get; }
	public Dependency[] BeginDependencies { get; }
	public Dependency[] EndDependencies { get; }

	internal RenderingInfo RenderingInfo { get; }
	internal DependencyInfo BeginDependencyInfo {get; }
	internal DependencyInfo EndDependencyInfo { get; }

	protected override void Free()
	{
		renderer.ToBeDisposed(RenderingInfo);
		renderer.ToBeDisposed(BeginDependencyInfo);
		renderer.ToBeDisposed(EndDependencyInfo);
	}

	public RenderTarget(int width, int height, Attachment[] colorAttachments, Attachment? depthAttachment, Attachment? stencilAttachment, Dependency[] beginDependencies, Dependency[] endDependencies)
	{
		if (width <= 0 || height <= 0)
			throw new ArgumentOutOfRangeException();

		this.Width = width;
		this.Height = height;

		this.ColorAttachments = colorAttachments ?? throw new ArgumentNullException();
		this.DepthAttachment = depthAttachment;
		this.StencilAttachment = stencilAttachment;
		this.BeginDependencies = beginDependencies ?? throw new ArgumentNullException();
		this.EndDependencies = endDependencies ?? throw new ArgumentNullException();

		this.RenderingInfo = new RenderingInfo(
			next: default,
			flags: default,
			renderArea: new(offset: new(0, 0), extent: new((uint)this.Width, (uint)this.Height)),
			layerCount: 1,
			viewMask: 0,
			colorAttachments: this.ColorAttachments.Select(x => new RenderingAttachmentInfo(
					next: default,
					imageView: x.Texture.ImageView,
					imageLayout: ImageLayout.ColorAttachmentOptimal,
					resolveMode: ResolveMode.None,
					resolveImageView: null,
					resolveImageLayout: ImageLayout.Undefined,
					loadOp: x.LoadOp,
					storeOp: x.StoreOp,
					clearValue: x.ClearValue
				)
			).ToArray(),
			depthAttachment: (this.DepthAttachment != null)
				? new RenderingAttachmentInfo(
					next: default,
					imageView: this.DepthAttachment.Texture.ImageView,
					imageLayout: ImageLayout.DepthAttachmentOptimal,
					resolveMode: ResolveMode.None,
					resolveImageView: null,
					resolveImageLayout: ImageLayout.Undefined,
					loadOp: this.DepthAttachment.LoadOp,
					storeOp: this.DepthAttachment.StoreOp,
					clearValue: this.DepthAttachment.ClearValue
				)
				: null,
			stencilAttachment: (this.StencilAttachment != null)
				? new RenderingAttachmentInfo(
					next: default,
					imageView: this.StencilAttachment.Texture.ImageView,
					imageLayout: ImageLayout.StencilAttachmentOptimal,
					resolveMode: ResolveMode.None,
					resolveImageView: null,
					resolveImageLayout: ImageLayout.Undefined,
					loadOp: this.StencilAttachment.LoadOp,
					storeOp: this.StencilAttachment.StoreOp,
					clearValue: this.StencilAttachment.ClearValue
				)
				: null
		);

		this.BeginDependencyInfo = new DependencyInfo(
			next: default,
			dependencyFlags: default,
			memoryBarriers: null,
			bufferMemoryBarriers: null,
			imageMemoryBarriers: this.BeginDependencies.Select(x => new ImageMemoryBarrier2(
					next: default,
					srcStage: x.SourceStage,
					srcAccess: x.SourceAccess,
					dstStage: x.DestinationStage,
					dstAccess: x.DestinationAccess,
					oldLayout: x.OldLayout,
					newLayout: x.NewLayout,
					srcQueueFamilyIndex: ~0u,
					dstQueueFamilyIndex: ~0u,
					image: this.ColorAttachments[x.Attachment].Texture.Image,
					subresourceRange: new ImageSubresourceRange(this.ColorAttachments[x.Attachment].Texture.Aspect, 0, 1, 0, 1)
				)
			).ToArray()
		);

		this.EndDependencyInfo = new DependencyInfo(
			next: default,
			dependencyFlags: default,
			memoryBarriers: null,
			bufferMemoryBarriers: null,
			imageMemoryBarriers: this.EndDependencies.Select(x => new ImageMemoryBarrier2(
					next: default,
					srcStage: x.SourceStage,
					srcAccess: x.SourceAccess,
					dstStage: x.DestinationStage,
					dstAccess: x.DestinationAccess,
					oldLayout: x.OldLayout,
					newLayout: x.NewLayout,
					srcQueueFamilyIndex: ~0u,
					dstQueueFamilyIndex: ~0u,
					image: this.ColorAttachments[x.Attachment].Texture.Image,
					subresourceRange: new ImageSubresourceRange(this.ColorAttachments[x.Attachment].Texture.Aspect, 0, 1, 0, 1)
				)
			).ToArray()
		);
	}

	public record Attachment(
		Texture Texture,
		AttachmentLoadOp LoadOp,
		AttachmentStoreOp StoreOp,
		ClearValue ClearValue,
		PipelineColorBlendAttachmentState? Blending
	);

	public record Dependency(
		uint Attachment,
		PipelineStage2 SourceStage,
		Access2 SourceAccess,
		PipelineStage2 DestinationStage,
		Access2 DestinationAccess,
		ImageLayout OldLayout,
		ImageLayout NewLayout
	);
}

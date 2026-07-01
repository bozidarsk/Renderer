using System;

using Vulkan;

namespace Renderer;

public class RenderTarget
{
	public int Width { get; }
	public int Height { get; }

	public Attachment[] ColorAttachments { get; }
	public Attachment? DepthAttachment { get; }
	public Attachment? StencilAttachment { get; }
	public Dependency[] BeginDependencies { get; }
	public Dependency[] EndDependencies { get; }

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
		PipelineStage2 SrcStage,
		Access2 SrcAccess,
		PipelineStage2 DstStage,
		Access2 DstAccess,
		ImageLayout OldLayout,
		ImageLayout NewLayout
	);
}

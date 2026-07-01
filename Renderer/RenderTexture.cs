using Vulkan;

namespace Renderer;

public class RenderTexture : RenderTarget
{
	public Texture ColorTexture => ColorAttachments[0].Texture;
	public Texture DepthTexture => DepthAttachment!.Texture;

	public RenderTexture(int width, int height) : base(
		width,
		height,
		colorAttachments: [
			new(
				new Texture(width, height, format: Format.R8G8B8A8SRGB, usage: ImageUsage.ColorAttachment | ImageUsage.Sampled, aspect: ImageAspect.Color, initialLayout: ImageLayout.ShaderReadOnlyOptimal),
				AttachmentLoadOp.Clear,
				AttachmentStoreOp.Store,
				new ClearValue(new ClearColorValue(0f, 0f, 0f, 0f)),
				null
			)
		],
		depthAttachment: new(
			new Texture(width, height, format: Format.D32SFloat, usage: ImageUsage.DepthStencilAttachment, aspect: ImageAspect.Depth, initialLayout: ImageLayout.DepthAttachmentOptimal),
			AttachmentLoadOp.Clear,
			AttachmentStoreOp.DontCare,
			new(new ClearDepthStencilValue(depth: 0, stencil: 0)),
			null
		),
		stencilAttachment: null,
		beginDependencies:
		[
			new(
				0,
				PipelineStage2.None,
				Access2.None,
				PipelineStage2.ColorAttachmentOutput,
				Access2.ColorAttachmentWrite,
				ImageLayout.ShaderReadOnlyOptimal,
				ImageLayout.ColorAttachmentOptimal
			)
		],
		endDependencies:
		[
			new(
				0,
				PipelineStage2.ColorAttachmentOutput,
				Access2.ColorAttachmentWrite,
				PipelineStage2.BottomOfPipe,
				Access2.None,
				ImageLayout.ColorAttachmentOptimal,
				ImageLayout.ShaderReadOnlyOptimal
			)
		]
	)
	{
	}
}

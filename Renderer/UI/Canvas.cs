using System;
using System.Collections.Generic;
using System.Linq;

using Vulkan;
using Renderer;

namespace Renderer.UI;

public class Canvas : SceneObject
{
	public int Width { private set; get; }
	public int Height { private set; get; }
	public float Scale { private set; get; } = 1;

	public CameraLayer CameraLayer
	{
		set
		{
			field = value;
			camera.Layer = value;
		}
		get;
	} = CameraLayer.UI;

	public CameraLayer TextureLayer
	{
		set
		{
			field = value;
			canvasTexture.Layer = value;
		}
		get;
	} = CameraLayer.Main;

	private readonly Camera camera;
	private readonly SceneObject canvasTexture;

	private readonly Vulkan.Buffer maskBuffer;
	private readonly Vulkan.DeviceMemory maskMemory;
	private readonly nint maskLocation;

	public void Resize(int width, int height, float scale)
	{
		this.Width = width;
		this.Height = height;
		this.Scale = scale;

		float w = Scale * (Width / 2f);
		float h = Scale * (Height / 2f);

		var projection = Matrix4x4.Orthographic(
			left: -w,
			right: w,
			bottom: -h,
			top: h,
			near: -1,
			far: 1
		);

		camera.Projection = projection;
		camera.Target = new(
			Width,
			Height,
			colorAttachments:
			[
				new(
					new Texture(Width, Height, format: Format.R8G8B8A8SRGB, usage: ImageUsage.ColorAttachment | ImageUsage.Sampled, aspect: ImageAspect.Color, initialLayout: ImageLayout.ShaderReadOnlyOptimal),
					AttachmentLoadOp.Clear,
					AttachmentStoreOp.Store,
					new ClearValue(new ClearColorValue(0f, 0f, 0f, 0f)),
					null
				),
				new(
					new Texture(Width, Height, format: Format.R8G8B8A8UInt, usage: ImageUsage.ColorAttachment | ImageUsage.TransferSrc, aspect: ImageAspect.Color, initialLayout: ImageLayout.TransferSrcOptimal),
					AttachmentLoadOp.Clear,
					AttachmentStoreOp.Store,
					new ClearValue(new ClearColorValue(0u, 0u, 0u, 0u)),
					new(
						blendEnable: false,
						srcColorBlendFactor: default,
						dstColorBlendFactor: default,
						colorBlendOp: default,
						srcAlphaBlendFactor: default,
						dstAlphaBlendFactor: default,
						alphaBlendOp: default,
						colorWriteMask: ColorComponent.R | ColorComponent.G | ColorComponent.B | ColorComponent.A
					)
				)
			],
			depthAttachment: null,
			stencilAttachment: null,
			beginDependencies: [
				new(
					0,
					PipelineStage2.None,
					Access2.None,
					PipelineStage2.ColorAttachmentOutput,
					Access2.ColorAttachmentWrite,
					ImageLayout.ShaderReadOnlyOptimal,
					ImageLayout.ColorAttachmentOptimal
				),
				new(
					1,
					PipelineStage2.None,
					Access2.None,
					PipelineStage2.ColorAttachmentOutput,
					Access2.ColorAttachmentWrite,
					ImageLayout.TransferSrcOptimal,
					ImageLayout.ColorAttachmentOptimal
				)
			],
			endDependencies: [
				new(
					0,
					PipelineStage2.ColorAttachmentOutput,
					Access2.ColorAttachmentWrite,
					PipelineStage2.BottomOfPipe,
					Access2.None,
					ImageLayout.ColorAttachmentOptimal,
					ImageLayout.ShaderReadOnlyOptimal
				),
				new(
					1,
					PipelineStage2.ColorAttachmentOutput,
					Access2.ColorAttachmentWrite,
					PipelineStage2.BottomOfPipe,
					Access2.None,
					ImageLayout.ColorAttachmentOptimal,
					ImageLayout.TransferSrcOptimal
				)
			]
		);

		canvasTexture.GetComponent<MeshRenderer>().Material["texture0"] = camera.Target.ColorAttachments[0].Texture;
	}

	private uint SampleId((double x, double y) position) => SampleId((int)position.x, (int)position.y);
	private uint SampleId((int x, int y) position) => SampleId(position.x, position.y);
	private uint SampleId(int x, int y)
	{
		var textureData = this.Scene.Renderer.AssetManager.GetTextureData(camera.Target!.ColorAttachments[1].Texture);

		CommandBuffer cmd = this.Scene.Renderer.BeginSingleTimeCommand();
		cmd.CopyImageToBuffer(textureData.Image, maskBuffer, ImageLayout.TransferSrcOptimal, new BufferImageCopy(
				bufferOffset: 0,
				bufferRowLength: 0,
				bufferImageHeight: 0,
				imageSubresource: new(
					aspect: ImageAspect.Color,
					mipLevel: 0,
					baseArrayLayer: 0,
					layerCount: 1
				),
				imageOffset: new(x: x, y: y, z: 0),
				imageExtent: new(width: 1, height: 1, depth: 1)
			)
		);
		this.Scene.Renderer.EndSingleTimeCommand(cmd);

		uint id;
		unsafe { id = *((uint*)maskLocation); }

		return id;
	}

	public override void Dispose()
	{
		canvasTexture.Dispose();
		camera.Dispose();
		maskBuffer.Dispose();
		maskMemory.Dispose();
		base.Dispose();
	}

	public Canvas(Scene scene) : this(scene, []) { }
	public Canvas(Scene scene, params Component[] components) : base(scene, components)
	{
		base.Layer = CameraLayer.None;

		camera = new Camera(base.Scene) { Layer = this.CameraLayer };

		CanvasVertex[] vertices = [
			new() { Position = new(-1, 1, 0), UV = new(0, 0) },
			new() { Position = new(1, 1, 0), UV = new(1, 0) },
			new() { Position = new(1, -1, 0), UV = new(1, 1) },
			new() { Position = new(-1, -1, 0), UV = new(0, 1) }
		];

		byte[] indices = [0, 2, 1, 2, 0, 3];

		canvasTexture = new SceneObject(this.Scene,
			new Transform(),
			new MeshFilter(new Mesh<CanvasVertex, byte>(vertices, indices)),
			new MeshRenderer(new Material(new ShaderProgram("Renderer/Shaders/canvas.vert.hlsl", "Renderer/Shaders/canvas.frag.hlsl")))
		)
		{ Layer = this.TextureLayer };

		var extent = scene.Renderer.SwapchainExtent;
		Resize((int)extent.Width, (int)extent.Height, this.Scale);

		DeviceSize size = sizeof(uint);
		this.Scene.Renderer.CreateBuffer(size, BufferUsage.TransferDst, out maskBuffer);
		this.Scene.Renderer.CreateBufferMemory(maskBuffer, MemoryProperty.HostVisible | MemoryProperty.HostCoherent, out maskMemory);
		maskLocation = maskMemory.Map(size: size, offset: default, flags: default);

		this.Scene.Window.OnMouseButton += (s, e) =>
		{
			uint id = SampleId(this.Scene.Window.CursorPosition);
			Console.WriteLine($"id: {id}");

			if (id == 0)
				return;

			foreach (var x in this.Children.OfType<UIObject>())
				x.RaiseEvent(new(EventType.MouseButton, EventPropagationType.Tunnel, s, e, id));
		};

		this.Scene.Window.OnFramebufferSize += (s, e) => Resize(scene.Size.x, scene.Size.y, this.Scale);

		AddChild(canvasTexture);
		AddChild(camera);
	}
}

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
	public float Scale { private set; get; } = 0.01f;

	public UIObject? Root { get; } = null;

	public CameraLayer CameraLayer
	{
		set
		{
			field = value;

			renderCamera.Layer = value;
			maskCamera.Layer = value;
			base.Layer = value;
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

	private Camera renderCamera, maskCamera;
	private SceneObject canvasTexture;

	private Vulkan.Buffer maskBuffer;
	private Vulkan.DeviceMemory maskMemory;
	private nint maskLocation;

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

		renderCamera.Texture = new(Width, Height, Format.R8G8B8A8SRGB);
		renderCamera.Projection = projection;

		maskCamera.Texture = new(Width, Height, Format.R8G8B8A8UInt); // R8G8B8A8UInt
		maskCamera.Projection = projection;

		canvasTexture.GetComponent<MeshRenderer>().Material["texture0"] = renderCamera.Texture;
	}

	private uint SampleId((double x, double y) position) => SampleId((int)position.x, (int)position.y);
	private uint SampleId((int x, int y) position) => SampleId(position.x, position.y);
	private uint SampleId(int x, int y)
	{
		var textureData = this.Scene.Renderer.AssetManager.GetTextureData(maskCamera.Texture!);

		CommandBuffer cmd = this.Scene.Renderer.BeginSingleTimeCommand();
		this.Scene.Renderer.TransitionImageLayout(textureData.Image, ImageLayout.ShaderReadOnlyOptimal, ImageLayout.TransferSrcOptimal, ImageAspect.Color, cmd);
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
		this.Scene.Renderer.TransitionImageLayout(textureData.Image, ImageLayout.TransferSrcOptimal, ImageLayout.ShaderReadOnlyOptimal, ImageAspect.Color, cmd);
		this.Scene.Renderer.EndSingleTimeCommand(cmd);

		uint id;
		unsafe { id = *((uint*)maskLocation); }

		return id;
	}

	public override void Dispose()
	{
		canvasTexture.Dispose();
		renderCamera.Dispose();
		maskCamera.Dispose();
		maskBuffer.Dispose();
		maskMemory.Dispose();
		base.Dispose();
	}

	public Canvas(Scene scene) : this(scene, []) { }
	public Canvas(Scene scene, params Component[] components) : base(scene, components)
	{
		renderCamera = new Camera(base.Scene) { Layer = this.CameraLayer };
		maskCamera = new Camera(base.Scene) { Layer = this.CameraLayer, MaskUIObjects = true };

		CanvasVertex[] vertices = [
			new() { Position = new(-1, 1, 0), UV = new(0, 0) },
			new() { Position = new(1, 1, 0), UV = new(1, 0) },
			new() { Position = new(1, -1, 0), UV = new(1, 1) },
			new() { Position = new(-1, -1, 0), UV = new(0, 1) }
		];

		byte[] indices = [0, 1, 2, 2, 3, 0];

		canvasTexture = new SceneObject(this.Scene,
			new Transform(),
			new MeshFilter(new Mesh<CanvasVertex, byte>(vertices, indices)),
			new MeshRenderer(new Material(new ShaderProgram("Renderer/Shaders/canvas.vert.hlsl", "Renderer/Shaders/canvas.frag.hlsl")))
		)
		{ Layer = this.TextureLayer };

		(int width, int height) = this.Scene.Window.Size;
		Resize(width, height, this.Scale);

		DeviceSize size = sizeof(uint);
		this.Scene.Renderer.CreateBuffer(size, BufferUsage.TransferDst, out maskBuffer);
		this.Scene.Renderer.CreateBufferMemory(maskBuffer, MemoryProperty.HostVisible | MemoryProperty.HostCoherent, out maskMemory);
		maskLocation = maskMemory.Map(size: size, offset: default, flags: default);

		this.Scene.Window.OnMouseButton += (s, e) =>
		{
			uint id = SampleId(this.Scene.Window.CursorPosition);

			if (id == 0)
				return;

			this.Root?.RaiseEvent(new(EventType.MouseButton, EventPropagationType.Direct, s, e, id));
		};
	}
}

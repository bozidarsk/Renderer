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
	public float Scale { set; get; } = 0.01f;

	new public CameraLayer Layer 
	{
		set 
		{
			renderCamera.Layer = value;
			maskCamera.Layer = value;
			base.Layer = value;
		}
		get => base.Layer;
	}

	private Camera renderCamera, maskCamera;
	private SceneObject canvasTexture;

	private Vulkan.Buffer maskBuffer;
	private Vulkan.DeviceMemory maskMemory;
	private nint maskLocation;

	private uint nextId = 0;
	internal uint NextId 
	{
		get 
		{
			if (nextId == uint.MaxValue)
				throw new InvalidOperationException("Canvas ran out of ui element ids.");

			return ++nextId;
		}
	}

	private List<UIObject> objects = new();
	internal void RegisterObject(UIObject x) => objects.Add(x);
	internal void UnregisterObject(UIObject x) => objects.Remove(x);

	private void Resize() 
	{
		(this.Width, this.Height) = this.Scene.Window.Size;

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

		renderCamera.Texture?.Dispose();
		renderCamera.Texture = new(this.Scene.Program, Width, Height, Format.R8G8B8A8SRGB);
		renderCamera.Projection = projection;

		maskCamera.Texture?.Dispose();
		maskCamera.Texture = new(this.Scene.Program, Width, Height, Format.R8G8B8A8UInt); // R8G8B8A8UInt
		maskCamera.Projection = projection;

		canvasTexture.GetComponent<MeshRenderer>().Material["texture0"] = renderCamera.Texture;
	}

	private uint SampleId((double x, double y) position) => SampleId((int)position.x, (int)position.y);
	private uint SampleId((int x, int y) position) => SampleId(position.x, position.y);
	private uint SampleId(int x, int y) 
	{
		CommandBuffer cmd = this.Scene.Program.BeginSingleTimeCommand();
		this.Scene.Program.TransitionImageLayout(maskCamera.Texture!.Image, ImageLayout.ShaderReadOnlyOptimal, ImageLayout.TransferSrcOptimal, cmd);
		cmd.CopyImageToBuffer(maskCamera.Texture!.Image, maskBuffer, ImageLayout.TransferSrcOptimal, new BufferImageCopy(
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
		this.Scene.Program.TransitionImageLayout(maskCamera.Texture!.Image, ImageLayout.TransferSrcOptimal, ImageLayout.ShaderReadOnlyOptimal, cmd);
		this.Scene.Program.EndSingleTimeCommand(cmd);

		uint id;
		unsafe { id = *((uint*)maskLocation); }

		return id;
	}

	public override void Dispose() 
	{
		renderCamera.Dispose();
		renderCamera.Texture!.Dispose();
		maskCamera.Dispose();
		maskCamera.Texture!.Dispose();
		maskBuffer.Dispose();
		maskMemory.Dispose();
		base.Dispose();
	}

	public Canvas(Scene scene) : this(scene, []) {}
	public Canvas(Scene scene, params Component[] components) : base(scene, components)
	{
		renderCamera = new Camera(base.Scene) { Layer = this.Layer };
		maskCamera = new Camera(base.Scene) { Layer = this.Layer, MaskUIObjects = true };

		CanvasVertex[] vertices = [
			new() { Position = new(-1, 1, 0), UV = new(0, 0) },
			new() { Position = new(1, 1, 0), UV = new(1, 0) },
			new() { Position = new(1, -1, 0), UV = new(1, 1) },
			new() { Position = new(-1, -1, 0), UV = new(0, 1) }
		];

		byte[] indices = [ 0, 1, 2, 2, 3, 0 ];

		canvasTexture = new SceneObject(this.Scene,
			new Transform(),
			new MeshFilter(new Mesh<CanvasVertex, byte>(this.Scene.Program, vertices, indices)),
			new MeshRenderer(Material.FromShaders(vertex: "Renderer/Shaders/canvas.vert.hlsl", fragment: "Renderer/Shaders/canvas.frag.hlsl"))
		) { Layer = CameraLayer.Main }; // TODO: user can change Layer

		Resize();

		DeviceSize size = sizeof(uint);
		this.Scene.Program.CreateBuffer(size, BufferUsage.TransferDst, out maskBuffer);
		this.Scene.Program.CreateBufferMemory(maskBuffer, MemoryProperty.HostVisible | MemoryProperty.HostCoherent, out maskMemory);
		maskLocation = maskMemory.Map(size: size, offset: default, flags: default);

		this.Scene.Window.OnMouseButton += (s, e) => 
		{
			uint id = SampleId(this.Scene.Window.CursorPosition);

			if (id == 0)
				return;

			var element = objects.Single(x => x.Id == id);
			element.RaiseMouseButtonEvent(this, e);
		};
	}
}

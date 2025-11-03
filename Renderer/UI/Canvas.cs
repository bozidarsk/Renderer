using Vulkan;
using Renderer;

namespace Renderer.UI;

public class Canvas : Camera
{
	public int Width { private set; get; }
	public int Height { private set; get; }
	public float Scale { set; get; } = 0.01f;

	private SceneObject canvasTexture;

	private void Recreate() 
	{
		(this.Width, this.Height) = this.Scene.Window.Size;

		float w = Scale * (Width / 2f);
		float h = Scale * (Height / 2f);

		base.Projection = Matrix4x4.Orthographic(
			left: -w,
			right: w,
			bottom: -h,
			top: h,
			near: -1,
			far: 1
		);

		base.Texture?.Dispose();
		base.Texture = new(this.Scene.Program, Width, Height, Format.R8G8B8A8SRGB);

		canvasTexture.GetComponent<MeshRenderer>().Material["texture0"] = base.Texture;
	}

	protected override void Awake() 
	{
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
		) { Layer = CameraLayer.Main };

		Recreate();
	}

	#pragma warning disable CS8618
	public Canvas(Scene scene) : base(scene) {}
	public Canvas(Scene scene, params Component[] components) : base(scene, components) {}
	#pragma warning restore
}

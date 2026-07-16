using Renderer;

namespace Renderer.UI;

public class Rectangle : UIObject
{
	private readonly MeshFilter meshFilter;
	private readonly MeshRenderer meshRenderer;

	public Color Color
	{
		set
		{
			field = value;
			meshRenderer.Material["COLOR"] = value;
		}
		get;
	}

	public Rectangle(Scene scene) : this(scene, []) { }
	public Rectangle(Scene scene, params Component[] components) : base(scene, components)
	{
		RectangleVertex[] vertices = [
			new() { Position = new(-1, 1, 0) },
			new() { Position = new(1, 1, 0) },
			new() { Position = new(1, -1, 0) },
			new() { Position = new(-1, -1, 0) },
		];

		byte[] indices = [0, 2, 1, 2, 0, 3];

		this.meshFilter = new MeshFilter(new Mesh<RectangleVertex, byte>(vertices, indices));
		this.meshRenderer = new MeshRenderer(new Material(new ShaderProgram("Renderer/Shaders/rectangle.vert.hlsl", "Renderer/Shaders/rectangle.frag.hlsl")));

		AddComponent(meshFilter);
		AddComponent(meshRenderer);

		this.Color = Color.White;
	}

	private struct RectangleVertex : IVertex
	{
		public Vector3 Position { set; get; }
		public Vector3 Normal { set { } }
		public Vector2 UV { set { } }
	}
}

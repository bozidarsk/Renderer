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

	public Rectangle(Scene scene) : base(scene,
		new RectTransform(),
		new MeshFilter(
			new Mesh<RectangleVertex, byte>(
				[
					new() { Position = new(-1, 1, 0) },
					new() { Position = new(1, 1, 0) },
					new() { Position = new(1, -1, 0) },
					new() { Position = new(-1, -1, 0) },
				],
				[0, 2, 1, 2, 0, 3]
			)
		),
		new MeshRenderer(
			new Material(
				shaders: ["Renderer/Shaders/rectangle.vert.hlsl", "Renderer/Shaders/rectangle.frag.hlsl"],
				uniforms: [new("COLOR", typeof(Color), Color.White)]
			)
		)
	)
	{
		this.meshFilter = GetComponent<MeshFilter>();
		this.meshRenderer = GetComponent<MeshRenderer>();

		this.Color = Color.White;
	}

	private struct RectangleVertex : IVertex
	{
		public Vector3 Position { set; get; }
		public Vector3 Normal { set { } }
		public Vector2 UV { set { } }
	}
}

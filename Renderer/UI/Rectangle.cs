using Renderer;

namespace Renderer.UI;

public class Rectangle : UIObject
{
	private readonly RectTransform rectTransform;
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

	public float Width
	{
		set
		{
			rectTransform.Rect = new(x: -value / 2f, y: this.Height / 2f, width: value, this.Height);
			rectTransform.LocalScale = new(value, this.Height, 1);

			GetCanvas()?.ComputeLayout();
		}
		get => rectTransform.Rect.Width;
	}

	public float Height
	{
		set
		{
			rectTransform.Rect = new(x: -this.Width / 2f, y: value / 2f, width: this.Width, height: value);
			rectTransform.LocalScale = new(this.Width, value, 1);

			GetCanvas()?.ComputeLayout();
	 	}
		get => rectTransform.Rect.Height;
	}

	public Rectangle(Scene scene) : base(scene,
		new RectTransform(),
		new MeshFilter(
			new Mesh<RectangleVertex, byte>(
				[
					new() { Position = new(-0.5f, 0.5f, 0) },
					new() { Position = new(0.5f, 0.5f, 0) },
					new() { Position = new(0.5f, -0.5f, 0) },
					new() { Position = new(-0.5f, -0.5f, 0) },
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
		this.rectTransform = GetComponent<RectTransform>();
		this.meshRenderer = GetComponent<MeshRenderer>();

		this.Color = Color.White;

		var defaultSize = new Vector2(50, 50);
		rectTransform.Rect = new(x: -defaultSize.x / 2f, y: defaultSize.y / 2f, width: defaultSize.x, defaultSize.y);
		rectTransform.LocalScale = new(defaultSize.x, defaultSize.y, 1);
	}

	private struct RectangleVertex : IVertex
	{
		public Vector3 Position { set; get; }
		public Vector3 Normal { set { } }
		public Vector2 UV { set { } }
	}
}

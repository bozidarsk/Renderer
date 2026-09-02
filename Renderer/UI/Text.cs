using System;

using Renderer;

namespace Renderer.UI;

public sealed class Text : UIObject
{
	new public CameraLayer Layer
	{
		set
		{
			field = value;

			outer.Layer = value;
			inner.Layer = value;
			base.Layer = value;
		}
		get;
	}

	public required Font Font
	{
		set
		{
			if (value == null)
				throw new ArgumentNullException();

			field = value;
			RecreateMesh();
		}
		get;
	}

	public float FontSize
	{
		set
		{
			field = value;
			RecreateMesh();
		}
		get;
	}

	public string Value
	{
		set
		{
			if (value == null)
				throw new ArgumentNullException();

			field = value;
			RecreateMesh();
		}
		get;
	}

	public Color Color
	{
		set
		{
			field = value;

			outerRenderer.Material["COLOR"] = value;
			innerRenderer.Material["COLOR"] = value;
		}
		get;
	}

	private readonly UIObject outer, inner;

	private readonly MeshFilter outerFilter, innerFilter;
	private readonly MeshRenderer outerRenderer, innerRenderer;

	private readonly RectTransform rectTransform;

	private void RecreateMesh()
	{
		if (this.Font == null || this.Value == null)
			return;

		TextMesh mesh = this.Font.CreateMesh(this.Value, this.FontSize);
		outerFilter.Mesh = mesh.Outer;
		innerFilter.Mesh = mesh.Inner;

		rectTransform.Rect = mesh.Rect;

		GetCanvas()?.ComputeLayout();
	}

	public Text(Scene scene) : base(scene, new RectTransform())
	{
		outer = new UIObject(scene,
			new Transform(),
			new MeshFilter(Mesh.Empty),
			new MeshRenderer(new Material(shaders: ["Renderer/Shaders/text-outer.vert.hlsl", "Renderer/Shaders/text-outer.frag.hlsl"], uniforms: [new("COLOR", typeof(Color), Color.White)]))
		);

		inner = new UIObject(scene,
			new Transform(),
			new MeshFilter(Mesh.Empty),
			new MeshRenderer(new Material(shaders: ["Renderer/Shaders/text-inner.vert.hlsl", "Renderer/Shaders/text-inner.frag.hlsl"], uniforms: [new("COLOR", typeof(Color), Color.White)]))
		);

		AddChild(inner);
		AddChild(outer);

		outerFilter = outer.GetComponent<MeshFilter>();
		innerFilter = inner.GetComponent<MeshFilter>();
		outerRenderer = outer.GetComponent<MeshRenderer>();
		innerRenderer = inner.GetComponent<MeshRenderer>();

		rectTransform = GetComponent<RectTransform>();

		this.FontSize = 24;
		this.Value = "";
		this.Color = Color.White;
	}
}

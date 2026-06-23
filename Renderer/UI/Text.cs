using System;
using System.Linq;

using Renderer;

namespace Renderer.UI;

public sealed class Text : UIObject
{
	public required Font Font { set; get; }

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

	public required Color Color
	{
		set
		{
			field = value;

			outerRenderer.Material["COLOR"] = value;
			innerRenderer.Material["COLOR"] = value;
		}
		get;
	}

	public required string Value
	{
		set
		{
			if (value == null)
				throw new ArgumentNullException();

			field = value;

			TextMesh mesh = this.Font.CreateMesh(value);
			outerFilter.Mesh = mesh.Outer;
			innerFilter.Mesh = mesh.Inner;
		}
		get;
	}

	private readonly UIObject outer, inner;

	private readonly MeshFilter outerFilter, innerFilter;
	private readonly MeshRenderer outerRenderer, innerRenderer;

	public Text(Scene scene) : this(scene, []) { }
	public Text(Scene scene, params Component[] components) : base(scene, components)
	{
		var thisTransform = this.Transform;

		outer = new UIObject(scene,
			thisTransform,
			new MeshFilter(null!),
			new MeshRenderer(new Material(new ShaderProgram("Renderer/Shaders/TextOuter.vert.hlsl", "Renderer/Shaders/TextOuter.frag.hlsl")))
		)
		{ MaskMaterial = new Material(new ShaderProgram("Renderer/Shaders/TextOuter.vert.hlsl", "Renderer/Shaders/TextOuter-mask.frag.hlsl")) };

		inner = new UIObject(scene,
			thisTransform,
			new MeshFilter(null!),
			new MeshRenderer(new Material(new ShaderProgram("Renderer/Shaders/TextInner.vert.hlsl", "Renderer/Shaders/TextInner.frag.hlsl")))
		)
		{ MaskMaterial = new Material(new ShaderProgram("Renderer/Shaders/TextInner.vert.hlsl", "Renderer/Shaders/TextInner-mask.frag.hlsl")) };

		Add(inner);
		Add(outer);

		outerFilter = outer.GetComponent<MeshFilter>();
		innerFilter = inner.GetComponent<MeshFilter>();
		outerRenderer = outer.GetComponent<MeshRenderer>();
		innerRenderer = inner.GetComponent<MeshRenderer>();
	}
}

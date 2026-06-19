using System;
using System.Linq;

using Vulkan;
using Renderer;

namespace Renderer.UI;

public sealed class Text : SceneObject
{
	public required Font Font { set; get; }

	new public CameraLayer Layer
	{
		set
		{
			outer.Layer = value;
			inner.Layer = value;
			base.Layer = value;
		}
		get => base.Layer;
	}

	public required Color Color
	{
		set
		{
			outerRenderer.Material["COLOR"] = value;
			innerRenderer.Material["COLOR"] = value;
		}
	}

	public string Value
	{
		set
		{
			if (value == null)
				throw new ArgumentNullException();

			TextMesh mesh = this.Font.CreateMesh(this.Scene.Renderer, value);
			outerFilter.Mesh.Dispose();
			outerFilter.Mesh = mesh.Outer;
			innerFilter.Mesh.Dispose();
			innerFilter.Mesh = mesh.Inner;
		}
	}

	private UIObject outer, inner;

	private MeshFilter outerFilter, innerFilter;
	private MeshRenderer outerRenderer, innerRenderer;

	public Text(Canvas canvas) : this(canvas, []) { }
	public Text(Canvas canvas, params Component[] components) : base(canvas.Scene, components)
	{
		var thisTransform = this.Transform;

		outer = new UIObject(canvas,
			thisTransform,
			new MeshFilter(new Mesh(this.Scene.Renderer)),
			new MeshRenderer(new Material(ShaderProgram.FromFiles(canvas.Scene.Renderer, "Renderer/Shaders/TextOuter.vert.hlsl", "Renderer/Shaders/TextOuter.frag.hlsl")))
		)
		{ MaskMaterial = new Material(ShaderProgram.FromFiles(canvas.Scene.Renderer, "Renderer/Shaders/TextOuter.vert.hlsl", "Renderer/Shaders/TextOuter-mask.frag.hlsl")) };

		inner = new UIObject(canvas,
			thisTransform,
			new MeshFilter(new Mesh(this.Scene.Renderer)),
			new MeshRenderer(new Material(ShaderProgram.FromFiles(canvas.Scene.Renderer, "Renderer/Shaders/TextInner.vert.hlsl", "Renderer/Shaders/TextInner.frag.hlsl")))
		)
		{ MaskMaterial = new Material(ShaderProgram.FromFiles(canvas.Scene.Renderer, "Renderer/Shaders/TextInner.vert.hlsl", "Renderer/Shaders/TextInner-mask.frag.hlsl")) };

		outerFilter = outer.GetComponent<MeshFilter>();
		innerFilter = inner.GetComponent<MeshFilter>();
		outerRenderer = outer.GetComponent<MeshRenderer>();
		innerRenderer = inner.GetComponent<MeshRenderer>();
	}
}

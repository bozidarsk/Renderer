using System;
using System.Linq;

using Vulkan;

namespace Renderer;

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

			TextMesh mesh = this.Font.CreateMesh(this.Scene.Program, value);
			outerFilter.Mesh.Dispose();
			outerFilter.Mesh = mesh.Outer;
			innerFilter.Mesh.Dispose();
			innerFilter.Mesh = mesh.Inner;
		}
	}

	private SceneObject outer, inner;

	private MeshFilter outerFilter, innerFilter;
	private MeshRenderer outerRenderer, innerRenderer;

	public Text(Scene scene) : this(scene, []) {}
	public Text(Scene scene, params Component[] components) : base(scene, components)
	{
		var thisTransform = this.Transform;

		outer = new SceneObject(this.Scene,
			thisTransform,
			new MeshFilter(new Mesh(this.Scene.Program)),
			new MeshRenderer(Material.FromShaders(vertex: "Renderer/Shaders/TextOuter.vert.hlsl", fragment: "Renderer/Shaders/TextOuter.frag.hlsl"))
		);

		inner = new SceneObject(this.Scene,
			thisTransform,
			new MeshFilter(new Mesh(this.Scene.Program)),
			new MeshRenderer(Material.FromShaders(vertex: "Renderer/Shaders/TextInner.vert.hlsl", fragment: "Renderer/Shaders/TextInner.frag.hlsl"))
		);

		// outer.Enabled = false;

		outerFilter = outer.GetComponent<MeshFilter>();
		innerFilter = inner.GetComponent<MeshFilter>();
		outerRenderer = outer.GetComponent<MeshRenderer>();
		innerRenderer = inner.GetComponent<MeshRenderer>();
	}
}

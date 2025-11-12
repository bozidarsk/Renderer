using Vulkan;
using Renderer;
using GLFW;

namespace Renderer.UI;

public class UIObject : SceneObject
{
	public Canvas Canvas { set; get; }

	internal uint Id { init; get; }

	public required Material MaskMaterial { init; get; }
	private Material? normalMaterial;

	public event MouseButtonEventHandler? OnMouseButton;
	internal void RaiseMouseButtonEvent(object? sender, MouseButtonEventArgs args) => OnMouseButton?.Invoke(sender, args);

	internal void SwitchToMaskMaterial() 
	{
		var mr = GetComponent<MeshRenderer>();
		normalMaterial = mr.Material;
		mr.Material = MaskMaterial;
	}

	internal void SwitchToNormalMaterial() 
	{
		var mr = GetComponent<MeshRenderer>();

		if (normalMaterial == null) 
		{
			normalMaterial = mr.Material;
			return;
		}

		mr.Material = normalMaterial;
	}

	public override void Dispose() 
	{
		this.Canvas.UnregisterObject(this);
		base.Dispose();
	}

	public UIObject(Canvas canvas) : this(canvas, []) {}
	public UIObject(Canvas canvas, params Component[] components) : base(canvas.Scene, components)
	{
		this.Id = canvas.NextId;
		this.Canvas = canvas;
		canvas.RegisterObject(this);
	}
}

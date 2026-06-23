using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using GLFW;
using Renderer;

namespace Renderer.UI;

public class UIObject : SceneObject
{
	private static uint nextId = 0;
	internal uint Id { get; } = checked(++nextId);

	public Material? MaskMaterial { init; get; } = null;
	private Material? normalMaterial;

	public Action<UIObject, object?, MouseButtonEventArgs>? MouseButton;
	protected virtual void OnMouseButton(object? sender, MouseButtonEventArgs args) { }

	internal void SwitchToMaskMaterial()
	{
		if (MaskMaterial != null)
		{
			var mr = GetComponent<MeshRenderer>();
			normalMaterial = mr.Material;
			mr.Material = MaskMaterial;
		}
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

	private UIObject? FindTarget(uint id)
	{
		if (this.Id == id)
			return this;

		foreach (var child in this.Children.OfType<UIObject>())
		{
			var target = child.FindTarget(id);

			if (target != null)
				return target;
		}

		return null;
	}

	private IEnumerable<UIObject> GetAncestors()
	{
		SceneObject root = this;

		while (root.Parent != null && !(root.Parent is Canvas))
		{
			root = root.Parent;

			if (root is UIObject obj)
				yield return obj;
		}
	}

	private void Dispatch(EventType type, object? sender, EventArgs args)
	{
		switch (type)
		{
			case EventType.MouseButton:
				MouseButton?.Invoke(this, sender, (MouseButtonEventArgs)args);
				OnMouseButton(sender, (MouseButtonEventArgs)args);
				break;
		}
	}

	internal void RaiseEvent(UIEvent uiEvent)
	{
		var target = FindTarget(uiEvent.Target);

		if (target == null)
			return;

		IEnumerable<UIObject> chain = uiEvent.Propagation switch
		{
			EventPropagationType.Direct => [target],
			EventPropagationType.Bubble => new[] { target }.Concat(target.GetAncestors()),
			EventPropagationType.Tunnel => target.GetAncestors().Reverse().Append(target),
			_ => []
		};

		foreach (var x in chain)
			x.Dispatch(uiEvent.Type, uiEvent.Sender, uiEvent.Args);
	}

	public static bool operator ==(UIObject? a, UIObject? b) => a?.Id == b?.Id;
	public static bool operator !=(UIObject? a, UIObject? b) => a?.Id != b?.Id;

	public override bool Equals(object? other) => (other is UIObject obj) ? obj == this : false;
	public override int GetHashCode() => Id.GetHashCode();

	public UIObject(Scene scene) : this(scene, []) { }
	public UIObject(Scene scene, params Component[] components) : base(scene, components)
	{
		this.Layer = CameraLayer.UI;
	}
}

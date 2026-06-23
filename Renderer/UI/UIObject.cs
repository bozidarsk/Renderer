using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using GLFW;
using Renderer;

namespace Renderer.UI;

public class UIObject : SceneObject, IEnumerable<UIObject>
{
	public UIObject? Parent { private set; get; } = null;

	private static uint nextId = 0;
	internal uint Id { get; } = checked(++nextId);

	private readonly List<UIObject> children = new();
	public IReadOnlyList<UIObject> Children => children;

	public IEnumerator<UIObject> GetEnumerator() => children.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => children.GetEnumerator();

	public void Add(UIObject child)
	{
		if (child == null)
			throw new ArgumentNullException();

		child.Layer |= this.Layer;
		child.Parent = this;
		children.Add(child);
	}

	public void Remove(UIObject child)
	{
		if (child == null)
			throw new ArgumentNullException();

		child.Layer &= ~CameraLayer.None;
		child.Parent = null;
		children.Remove(child);
	}

	public Material? MaskMaterial { init; get; } = null;
	private Material? normalMaterial;

	public event MouseButtonEventHandler? MouseButton;
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

	private UIObject FindRoot()
	{
		var root = this;

		while (root.Parent != null)
			root = root.Parent;

		return root;
	}

	private UIObject? FindTarget(uint id)
	{
		if (this.Id == id)
			return this;

		foreach (var child in children)
		{
			var target = child.FindTarget(id);

			if (target != null)
				return target;
		}

		return null;
	}

	private IEnumerable<UIObject> GetAncestors()
	{
		var root = this;

		while (root.Parent != null)
		{
			root = root.Parent;
			yield return root;
		}
	}

	private void Dispatch(UIEvent uiEvent)
	{
		switch (uiEvent.Type)
		{
			case EventType.MouseButton:
				MouseButton?.Invoke(uiEvent.Sender, (MouseButtonEventArgs)uiEvent.Args);
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
			x.Dispatch(uiEvent);
	}

	public static bool operator ==(UIObject? a, UIObject? b) => a?.Id == b?.Id;
	public static bool operator !=(UIObject? a, UIObject? b) => a?.Id != b?.Id;

	public override bool Equals(object? other) => (other is UIObject obj) ? obj == this : false;
	public override int GetHashCode() => Id.GetHashCode();

	public UIObject(Scene scene) : this(scene, []) { }
	public UIObject(Scene scene, params Component[] components) : base(scene, components)
	{
		this.Layer = CameraLayer.UI;

		this.MouseButton += OnMouseButton;
	}
}

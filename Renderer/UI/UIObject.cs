using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using GLFW;
using Renderer;

namespace Renderer.UI;

public class UIObject : SceneObject, IEnumerable<UIObject>
{
	public UIObject? Parent { get; }

	private static uint nextId = 0;
	internal uint Id { get; } = checked(++nextId);

	private readonly List<UIObject> children = new();
	public IReadOnlyList<UIObject> Children => children;

	public IEnumerator<UIObject> GetEnumerator() => children.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => children.GetEnumerator();

	public void Add(UIObject child) => children.Add(child ?? throw new ArgumentNullException());
	public void Remove(UIObject child) => children.Remove(child ?? throw new ArgumentNullException());

	public required Material MaskMaterial { init; get; }
	private Material? normalMaterial;

	public event MouseButtonEventHandler? MouseButton;
	protected virtual void OnMouseButton(object? sender, MouseButtonEventArgs args) { }

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

	public UIObject(Canvas canvas) : this(canvas, []) { }
	public UIObject(Canvas canvas, params Component[] components) : base(canvas.Scene, components)
	{
		this.Layer = CameraLayer.UI;
		this.Parent = null;

		this.MouseButton += OnMouseButton;
	}

	public UIObject(UIObject parent) : this(parent, []) { }
	public UIObject(UIObject parent, params Component[] components) : base(parent.Scene, components)
	{
		this.Layer = CameraLayer.UI;
		this.Parent = parent;

		this.MouseButton += OnMouseButton;
	}
}

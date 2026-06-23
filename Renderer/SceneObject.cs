using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;

using Renderer.Physics;
using Renderer.UI;

namespace Renderer;

public class SceneObject : IDisposable, IEnumerable<SceneObject>
{
	public readonly Scene Scene;
	private List<Component> components = new();

	public CameraLayer Layer { set; get; } = CameraLayer.Main;
	public bool IsEnabled { set; get; } = true;

	public SceneObject? Parent { private set; get; } = null;

	private readonly List<SceneObject> children = [];
	public IReadOnlyList<SceneObject> Children => children;

	public IEnumerator<SceneObject> GetEnumerator() => children.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => children.GetEnumerator();

	public void Add(SceneObject child)
	{
		if (child == null)
			throw new ArgumentNullException();

		if (children.Contains(child))
			return;

		child.Parent = this;
		children.Add(child);
	}

	public void Remove(SceneObject child)
	{
		if (child == null)
			throw new ArgumentNullException();

		if (children.Remove(child))
			child.Parent = null;
	}

	public Transform Transform => TryGetComponent<Transform>(out Transform transform) ? transform : throw new InvalidOperationException("Object does not have a Transform component.");
	public bool IsRenderable => IsEnabled && HasComponents<Transform, MeshFilter, MeshRenderer>();

	internal Matrix4x4 Model
	{
		get
		{
			var model = Matrix4x4.Identity;

			for (var obj = this; obj != null; obj = obj.Parent)
				if (obj.TryGetComponent<Transform>(out Transform transform))
					model *= transform;

			return model;
		}
	}

	public Action? Awake;
	protected virtual void OnAwake() { }
	internal void RaiseAwake()
	{
		if (!IsEnabled)
			return;

		Awake?.Invoke();
		OnAwake();
	}

	public Action? Start;
	protected virtual void OnStart() { }
	internal void RaiseStart()
	{
		if (!IsEnabled)
			return;

		Start?.Invoke();
		OnStart();
	}

	public Action? Update;
	protected virtual void OnUpdate() { }
	internal void RaiseUpdate()
	{
		if (!IsEnabled)
			return;

		Update?.Invoke();
		OnUpdate();
	}

	public EventHandler<CollisionEventArgs>? Collision;
	protected virtual void OnCollision(object? sender, CollisionEventArgs args) { }
	internal void RaiseCollision(object? sender, CollisionEventArgs args)
	{
		if (!IsEnabled)
			return;

		Collision?.Invoke(sender, args);
		OnCollision(sender, args);
	}

	public void AddComponent<T>(T component) where T : Component => components.Add(component);
	public void AddComponents<T>(IEnumerable<T> components) where T : Component => this.components.AddRange(components);

	public bool HasComponent<T>() where T : Component => components.Any(x => x is T);
	public bool HasComponents<T1, T2>()
		where T1 : Component
		where T2 : Component
		=> HasComponent<T1>() && HasComponent<T2>()
	;
	public bool HasComponents<T1, T2, T3>()
		where T1 : Component
		where T2 : Component
		where T3 : Component
		=> HasComponent<T1>() && HasComponent<T2>() && HasComponent<T3>()
	;
	public bool HasComponents<T1, T2, T3, T4>()
		where T1 : Component
		where T2 : Component
		where T3 : Component
		where T4 : Component
		=> HasComponent<T1>() && HasComponent<T2>() && HasComponent<T3>() && HasComponent<T4>()
	;
	public bool HasComponents<T1, T2, T3, T4, T5>()
		where T1 : Component
		where T2 : Component
		where T3 : Component
		where T4 : Component
		where T5 : Component
		=> HasComponent<T1>() && HasComponent<T2>() && HasComponent<T3>() && HasComponent<T4>() && HasComponent<T5>()
	;

	public T GetComponent<T>() where T : Component
	{
		if (TryGetComponent(out T component))
			return component;

		throw new NullReferenceException($"Failed to get component of type {typeof(T)}.");
	}

	public IEnumerable<T> GetComponents<T>() where T : Component
	{
		if (TryGetComponents(out IEnumerable<T> components))
			return components;

		throw new NullReferenceException($"Failed to get components of type {typeof(T)}.");
	}

	public bool TryGetComponent<T>(out T component) where T : Component
	{
		component = (this.components.SingleOrDefault(x => x is T) as T)!;
		return component != null;
	}

	public bool TryGetComponents<T>(out IEnumerable<T> components) where T : Component
	{
		components = this.components.OfType<T>().ToArray();
		return components.Any();
	}

	public virtual void Dispose()
	{
		Parent?.Remove(this);

		while (children.Count > 0)
			children[0].Dispose();

		foreach (var x in components.OfType<IDisposable>())
			x.Dispose();
	}

	public SceneObject(Scene scene) => this.Scene = scene;
	public SceneObject(Scene scene, params Component[] components) : this(scene) => this.components.AddRange(components);
}

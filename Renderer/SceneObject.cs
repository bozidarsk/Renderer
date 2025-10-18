using System;
using System.Linq;
using System.Collections.Generic;

using Vulkan;
using Renderer.Physics;

namespace Renderer;

public class SceneObject : IDisposable
{
	public readonly Scene Scene;
	private List<Component> components = new();

	public bool Enabled { set; get; } = true;

	public Transform Transform => TryGetComponent<Transform>(out Transform transform) ? transform : throw new InvalidOperationException("Object does not have a Transform component.");
	public bool Renderable => Enabled && HasComponent<Transform>() && HasComponent<MeshFilter>() && HasComponent<MeshRenderer>();

	public RenderInfo RenderInfo 
	{
		get 
		{
			if (!Renderable)
				throw new InvalidOperationException("Cannot get RenderInfo for non-renderable object.");

			var mesh = GetComponent<MeshFilter>().Mesh;
			var material = GetComponent<MeshRenderer>().Material;

			return new(mesh.VertexBuffer, mesh.VertexCount, mesh.VertexType, mesh.IndexBuffer, mesh.IndexCount, mesh.IndexType, material.Shaders);
		}
	}

	public Action OnAwake;
	public Action OnStart;
	public Action OnUpdate;
	public Action<SceneObject, Collision> OnCollision;
	protected virtual void Awake() {}
	protected virtual void Start() {}
	protected virtual void Update() {}
	protected virtual void Collision(SceneObject other, Collision collision) {}

	public void AddComponent<T>(T component) where T : Component => components.Add(component);
	public void AddComponents<T>(IEnumerable<T> components) where T : Component => this.components.AddRange(components);

	public bool HasComponent<T>() where T : Component => components.Where(x => x is T).Any();

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
		component = (T)this.components.Where(x => x is T).SingleOrDefault()!;
		return component != null;
	}

	public bool TryGetComponents<T>(out IEnumerable<T> components) where T : Component
	{
		components = this.components.Where(x => x is T).Select(x => (T)x);
		return components.Any();
	}

	public void Dispose() 
	{
		this.Scene.UnregisterObject(this);

		foreach (var x in components.Where(x => x is IDisposable))
			((IDisposable)x).Dispose();
	}

	public SceneObject(Scene scene) 
	{
		this.OnAwake ??= () => this.Awake();
		this.OnStart ??= () => this.Start();
		this.OnUpdate ??= () => this.Update();
		this.OnCollision ??= (other, collision) => this.Collision(other, collision);
		this.Scene = scene;
		this.Scene.RegisterObject(this);
	}

	public SceneObject(Scene scene, params Component[] components) : this(scene) => this.components.AddRange(components);
}

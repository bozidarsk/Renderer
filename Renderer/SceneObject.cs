using System;
using System.Linq;
using System.Collections.Generic;

using Vulkan;
using Renderer.Physics;

namespace Renderer;

public class SceneObject : IDisposable, IRenderable
{
	public readonly Scene Scene;
	private List<Component> components = new();

	public CameraLayer Layer { set; get; } = CameraLayer.MainCamera;
	public bool IsEnabled { set; get; } = true;

	public Transform Transform => TryGetComponent<Transform>(out Transform transform) ? transform : throw new InvalidOperationException("Object does not have a Transform component.");
	public bool IsRenderable => IsEnabled && HasComponents<Transform, MeshFilter, MeshRenderer>();

	public Matrix4x4 Model => this.Transform;
	public IReadOnlyDictionary<string, object> Uniforms => TryGetComponent<MeshRenderer>(out MeshRenderer mr) ? mr.Material.Uniforms : throw new InvalidOperationException("Object does not have a MeshRenderer component.");
	Info IInfoProvider.Info => this.Info;
	public RenderInfo Info 
	{
		get 
		{
			if (!IsRenderable)
				throw new InvalidOperationException("Cannot get RenderInfo for non-renderable object.");

			var mesh = GetComponent<MeshFilter>().Mesh;
			var material = GetComponent<MeshRenderer>().Material;

			return new RenderInfo(mesh.VertexBuffer, mesh.VertexCount, mesh.VertexType, mesh.IndexBuffer, mesh.IndexCount, mesh.IndexType, material.Shaders);
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

	public bool HasComponent<T>() where T : Component => components.Any(x => x is T);
	public bool HasComponents<T1, T2>() 
		where T1 : Component
		where T2 : Component
		=> components.Any(x => (x is T1) || (x is T2))
	;
	public bool HasComponents<T1, T2, T3>() 
		where T1 : Component
		where T2 : Component
		where T3 : Component
		=> components.Any(x => (x is T1) || (x is T2) || (x is T3))
	;
	public bool HasComponents<T1, T2, T3, T4>() 
		where T1 : Component
		where T2 : Component
		where T3 : Component
		where T4 : Component
		=> components.Any(x => (x is T1) || (x is T2) || (x is T3) || (x is T4))
	;
	public bool HasComponents<T1, T2, T3, T4, T5>() 
		where T1 : Component
		where T2 : Component
		where T3 : Component
		where T4 : Component
		where T5 : Component
		=> components.Any(x => (x is T1) || (x is T2) || (x is T3) || (x is T4) || (x is T5))
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

	public void Dispose() 
	{
		this.Scene.UnregisterObject(this);

		foreach (var x in components.OfType<IDisposable>())
			x.Dispose();
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

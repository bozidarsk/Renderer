using System;
using System.Collections.Generic;
using System.Linq;

using GLFW;
using Vulkan;
using Renderer.Physics;

namespace Renderer;

public class Scene : IDisposable
{
	public readonly Renderer Renderer;
	public readonly GLFW.Window Window;

	private readonly List<SceneObject> objects = new();

	public event EventHandler<DebugUtilsMessengerEventArgs>? DebugUtilsMessageReceived;

	public void Run()
	{
		foreach (var x in objects)
			x.OnStart();

		while (!this.Window.ShouldClose)
		{
			Input.PollEvents();

			foreach (var x in objects)
				x.OnUpdate();

			PhysicsEngine.ResolveDynamics(objects.Where(x => x.IsEnabled && x.HasComponents<Transform, RigidBody>()));
			PhysicsEngine.ResolveCollisions(objects.Where(x => x.IsEnabled && x.HasComponent<Collider>()));

			var renderable = objects.Where(x => x.IsEnabled && x.IsRenderable && !(x is Camera)).ToArray();

			foreach (var camera in objects.OfType<Camera>())
				camera.Render(renderable.Where(x => (x.Layer & camera.Layer) != 0));
		}

		this.Renderer.DeviceWaitIdle();
	}

	internal void RegisterObject(SceneObject x)
	{
		x.OnAwake();
		objects.Add(x);
	}

	internal void UnregisterObject(SceneObject x) => objects.Remove(x);

	public void Dispose()
	{
		while (objects.Count > 0)
			objects[0].Dispose();

		this.Renderer.Dispose();
		this.Window.Dispose();
		GLFW.Program.Terminate();
	}

	public Scene() : this(1280, 720) { }
	public Scene(int width, int height)
	{
		if (!GLFW.Program.Initialize())
			throw new InvalidOperationException("GLFW failed to initialize.");

		Window.SetHint(Hint.ClientApi, 0);

		this.Window = new(width, height);
		this.Renderer = new(this.Window);

		this.Renderer.DebugUtilsMessageReceived += (s, e) => this.DebugUtilsMessageReceived?.Invoke(s, e);

		this.Renderer.Initialize();
	}
}

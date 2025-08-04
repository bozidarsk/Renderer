using System;
using System.Collections.Generic;
using System.Linq;

using GLFW;
using Vulkan;

namespace Renderer;

public class Scene : IDisposable
{
	public readonly Vulkan.Program Program;
	public readonly GLFW.Window Window;

	private readonly List<SceneObject> objects = new();

	public event DebugEventHandler? OnDebugMessage;

	public void Run() 
	{
		foreach (var x in objects)
			x.OnStart();

		while (!this.Window.ShouldClose) 
		{
			Input.PollEvents();

			foreach (var x in objects)
				x.OnUpdate();

			var renderable = objects.Where(x => x.Enabled && x.Renderable).ToArray();

			foreach (var camera in objects.Where(x => x is Camera))
				((Camera)camera).Render(renderable);
		}
	}

	internal void RegisterObject(SceneObject x) 
	{
		x.OnAwake();
		objects.Add(x);
	}

	internal void UnregisterObject(SceneObject x) => objects.Remove(x); 

	public void Dispose() 
	{
		this.Program.DeviceWaitIdle();

		foreach (var x in objects)
			x.Dispose();

		this.Program.Dispose();
		this.Window.Dispose();
		GLFW.Program.Terminate();
	}

	public Scene() : this(null) {}
	public Scene(int width, int height) : this(null, width, height) {}
	public Scene(DebugEventHandler? onDebugMessage, int width = 1280, int height = 720) 
	{
		if (!GLFW.Program.Initialize())
			throw new InvalidOperationException("GLFW failed to initialize.");

		Window.SetHint(Hint.ClientApi, 0);

		this.Window = new(width, height);
		this.Program = new(this.Window);

		this.OnDebugMessage += onDebugMessage;
		this.Program.OnDebugMessage += (s, e) => this.OnDebugMessage?.Invoke(s, e);

		this.Program.Initialize();
	}
}

using System;
using System.Collections.Generic;

namespace Renderer;

public class Camera : SceneObject
{
	public Matrix4x4 Projection { set; get; } = Matrix4x4.Perspective(fov: 60, ratio: 16f / 9f, near: 0.1f, far: 10);
	public RenderTarget? Target { set; get; } = null;

	internal void Render(params SceneObject[] objects) => Render(objects ?? throw new ArgumentNullException());
	internal void Render(IEnumerable<SceneObject> objects)
	{
		if (objects == null)
			throw new ArgumentNullException();

		this.Scene.Renderer.DrawFrame(
			projection: this.Projection,
			view: TryGetComponent<Transform>(out Transform transform) ? transform : Matrix4x4.Identity,
			objects: objects,
			target: this.Target
		);
	}

	public Camera(Scene scene) : base(scene) { }
	public Camera(Scene scene, params Component[] components) : base(scene, components) { }
}

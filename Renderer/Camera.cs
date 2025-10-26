using System;
using System.Linq;
using System.Collections.Generic;

using Vulkan;

namespace Renderer;

public sealed class Camera : SceneObject
{
	public Matrix4x4 Projection { set; get; } = Matrix4x4.Perspective(fov: 60, ratio: 16f/9f, near: 0.1f, far: 10);
	public RenderTexture? Texture { set; get; } = null;

	internal void Render(params SceneObject[] objects) => Render((objects is IEnumerable<SceneObject> x) ? x :  throw new ArgumentException());
	internal void Render(IEnumerable<SceneObject> objects) 
	{
		if (objects == null)
			throw new ArgumentNullException();

		if (this.Texture == null && base.Layer != CameraLayer.MainCamera)
			return;

		this.Scene.Program.DrawFrame(
			projection: this.Projection,
			view: this.Transform,
			objects: objects.OfType<IRenderable>(),
			texture: this.Texture?.Info as RenderTextureInfo
		);
	}

	public Camera(Scene scene) : base(scene) {}
	public Camera(Scene scene, params Component[] components) : base(scene, components) {}
}

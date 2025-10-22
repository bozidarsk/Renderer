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
			objects: objects.Select(x => ((Matrix4x4)x.Transform, x.GetComponent<MeshRenderer>().Material.Uniforms, x.RenderInfo)),
			texture: (this.Texture != null) ? (this.Texture.Extent, this.Texture.Framebuffer, this.Texture.Image) : null
		);
	}

	public Camera(Scene scene) : base(scene) {}
	public Camera(Scene scene, params Component[] components) : base(scene, components) {}
}

[Flags]
public enum CameraLayer 
{
	MainCamera = Camera0,
	Camera0 = 1 << 0,
	Camera1 = 1 << 1,
	Camera2 = 1 << 2,
	Camera3 = 1 << 3,
	Camera4 = 1 << 4,
	Camera5 = 1 << 5,
	Camera6 = 1 << 6,
	Camera7 = 1 << 7,
	Camera8 = 1 << 8,
	Camera9 = 1 << 9,
	Camera10 = 1 << 10,
	Camera11 = 1 << 11,
	Camera12 = 1 << 12,
	Camera13 = 1 << 13,
	Camera14 = 1 << 14,
	Camera15 = 1 << 15,
	Camera16 = 1 << 16,
	Camera17 = 1 << 17,
	Camera18 = 1 << 18,
	Camera19 = 1 << 19,
	Camera20 = 1 << 20,
	Camera21 = 1 << 21,
	Camera22 = 1 << 22,
	Camera23 = 1 << 23,
	Camera24 = 1 << 24,
	Camera25 = 1 << 25,
	Camera26 = 1 << 26,
	Camera27 = 1 << 27,
	Camera28 = 1 << 28,
	Camera29 = 1 << 29,
	Camera30 = 1 << 30,
	Camera31 = 1 << 31,
}

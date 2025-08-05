using System;
using System.Linq;
using System.Collections.Generic;

using Vulkan;

namespace Renderer;

public sealed class Camera : SceneObject
{
	public Matrix4x4 Projection { set; get; } = Matrix4x4.Perspective(fov: 60, ratio: 16f/9f, near: 1f, far: 10);
	// public Matrix4x4 View { set; get; } = Matrix4x4.LookAt(Vector3.One * 2, Vector3.Zero, Vector3.Up);

	// float x = 0;

	// protected override void Update() 
	// {
	// 	// Projection = Matrix4x4.Rotate(Quaternion.Euler(new(
	// 	// 	float.TryParse(global::Program.xText.Text, out float x) ? x : 0,
	// 	// 	float.TryParse(global::Program.yText.Text, out float y) ? y : 0,
	// 	// 	float.TryParse(global::Program.zText.Text, out float z) ? z : 0
	// 	// )));

	// 	// Projection = Matrix4x4.Perspective(
	// 	// 	fov: float.TryParse(global::Program.fovText.Text, out float fov) ? fov : 60,
	// 	// 	ratio: 16f/9f,
	// 	// 	near: float.TryParse(global::Program.nearText.Text, out float near) ? near : -1,
	// 	// 	far: float.TryParse(global::Program.farText.Text, out float far) ? far : 1
	// 	// );

	// 	// float x = float.TryParse(global::Program.fovText.Text, out float a) ? a : 5;
	// 	// Projection = Matrix4x4.Orthographic(
	// 	// 	left: -x, right: x, bottom: -x, top: x, near: -1, far: 1
	// 	// );

	// 	// Transform.Matrix = Matrix4x4.Identity.Inverse;
	// 	// x += 0.0001f;
	// }

	internal void Render(params SceneObject[] objects) => Render((objects is IEnumerable<SceneObject> x) ? x :  throw new ArgumentException());
	internal void Render(IEnumerable<SceneObject> objects) 
	{
		if (objects == null)
			throw new ArgumentNullException();

		this.Scene.Program.DrawFrame(
			projection: this.Projection,
			view: this.Transform,
			objects: objects.Select(x => ((Matrix4x4)x.Transform, x.GetComponent<MeshRenderer>().Material.Uniforms, x.RenderInfo))
		);
	}

	public Camera(Scene scene) : base(scene) {}
	public Camera(Scene scene, params Component[] components) : base(scene, components) {}
}

using System;
using System.Linq;
using System.Collections.Generic;

using Renderer.UI;

namespace Renderer;

public class Camera : SceneObject
{
	public Matrix4x4 Projection { set; get; } = Matrix4x4.Perspective(fov: 60, ratio: 16f / 9f, near: 0.1f, far: 10);
	public RenderTexture? Texture { set; get; } = null;
	public bool MaskUIObjects { set; get; } = false;

	internal void Render(params SceneObject[] objects) => Render(objects ?? throw new ArgumentNullException());
	internal void Render(IEnumerable<SceneObject> objects)
	{
		if (objects == null)
			throw new ArgumentNullException();

		var uiObjects = MaskUIObjects ? objects.OfType<UIObject>().ToArray() : null;

		if (uiObjects != null)
			foreach (var x in uiObjects)
				x.SwitchToMaskMaterial();

		this.Scene.Renderer.DrawFrame(
			projection: this.Projection,
			view: TryGetComponent<Transform>(out Transform transform) ? transform : Matrix4x4.Identity,
			objects: objects,
			texture: this.Texture
		);

		if (uiObjects != null)
			foreach (var x in uiObjects)
				x.SwitchToNormalMaterial();
	}

	public Camera(Scene scene) : base(scene) { }
	public Camera(Scene scene, params Component[] components) : base(scene, components) { }
}

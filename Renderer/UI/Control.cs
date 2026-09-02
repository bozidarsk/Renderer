using Renderer;

namespace Renderer.UI;

public abstract class Control : UIObject
{
	internal abstract void ComputeLayout();

	public Control(Scene scene) : base(scene, new RectTransform()) {}
}

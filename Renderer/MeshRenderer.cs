namespace Renderer;

public class MeshRenderer : Component
{
	public Material Material { set; get; }

	public MeshRenderer(Material material) => this.Material = material;
}

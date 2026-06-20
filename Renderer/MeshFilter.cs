using System;

namespace Renderer;

public class MeshFilter : Component
{
	public Mesh Mesh { set; get; }

	public MeshFilter(Mesh mesh) => this.Mesh = mesh;
}

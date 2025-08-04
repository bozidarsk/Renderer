using System;

namespace Renderer;

public class MeshFilter : Component, IDisposable
{
	public Mesh Mesh { set; get; }

	public void Dispose() => this.Mesh.Dispose();

	public MeshFilter(Mesh mesh) => this.Mesh = mesh;
}

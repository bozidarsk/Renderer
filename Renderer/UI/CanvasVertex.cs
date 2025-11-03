using System.Runtime.InteropServices;

using Vulkan;

namespace Renderer.UI;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CanvasVertex : IVertex 
{
	public Vector3 Position { set; get; }
	public Vector2 UV { set; get; }

	Vector3 IVertex.Normal { set {} }
}

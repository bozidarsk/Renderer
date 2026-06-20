using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Renderer;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DefaultVertex : IVertex
{
	public Vector3 Position { set; get; }
	public Vector3 Normal { set; get; }
	public Vector2 UV { set; get; }
	public Color Color { set; get; }

	public DefaultVertex(Vector3 position, Color color) => (this.Position, this.Color) = (position, color);
}

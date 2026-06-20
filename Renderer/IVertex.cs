using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Renderer;

public interface IVertex
{
	Vector3 Position { set; }
	Vector2 UV { set; }
	Vector3 Normal { set; }
}

using Vulkan;
using Renderer;

namespace Renderer.Physics;

public class RigidBody : Component
{
	public float Mass { set; get; }
	public Vector3 Velocity { set; get; }
	public Vector3 Gravity { set; get; }
	public Vector3 Force { set; get; }

	public void AddForce(Vector3 value, ForceMode mode = ForceMode.Force) 
	{
		switch (mode) 
		{
			case ForceMode.Force:
				Force += value;
				break;
			case ForceMode.Acceleration:
				Force += value * Mass;
				break;
			case ForceMode.Impulse:
				Velocity += value / Mass;
				break;
			case ForceMode.Velocity:
				Velocity += value;
				break;
		}
	}
}

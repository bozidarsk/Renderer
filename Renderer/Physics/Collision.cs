using Vulkan;

namespace Renderer.Physics;

public struct Collision 
{
	public required Vector3 Normal { get; init; }
	public bool IsSourceTrigger { internal set; get; }
	public bool IsTargetTrigger { internal set; get; }

	public override string ToString() => $"{{ Normal: {Normal} }}";
}

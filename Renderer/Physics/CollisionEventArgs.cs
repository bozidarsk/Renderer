using System;

namespace Renderer.Physics;

public sealed class CollisionEventArgs : EventArgs
{
	public SceneObject Other { get; }
	public Vector3 Normal { get; }
	public bool IsSourceTrigger { get; }
	public bool IsTargetTrigger { get; }

	public CollisionEventArgs(
		SceneObject other,
		Vector3 normal,
		bool isSourceTrigger,
		bool isTargetTrigger
	)
	{
		this.Other = other;
		this.Normal = normal;
		this.IsSourceTrigger = isSourceTrigger;
		this.IsTargetTrigger = isTargetTrigger;
	}
}

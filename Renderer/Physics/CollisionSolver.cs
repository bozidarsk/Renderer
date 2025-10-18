using Renderer;

namespace Renderer.Physics;

internal abstract class CollisionSolver 
{
	public abstract void Solve(SceneObject source, SceneObject target, Collision collision, float dt);
}

internal sealed class ImpulseSolver : CollisionSolver
{
	public override void Solve(SceneObject source, SceneObject target, Collision collision, float dt) 
	{
		if (!collision.IsSourceTrigger && !collision.IsTargetTrigger && target.TryGetComponent<RigidBody>(out RigidBody targetBody))
			targetBody.AddForce(collision.Normal, ForceMode.Impulse);
	}
}

internal sealed class TriggerSolver : CollisionSolver
{
	public override void Solve(SceneObject source, SceneObject target, Collision collision, float dt) 
	{
		if (collision.IsSourceTrigger)
			source.OnCollision(target, collision);
	}
}

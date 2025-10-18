using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Vulkan;
using Renderer;

namespace Renderer.Physics;

internal static class PhysicsEngine 
{
	private static CollisionSolver[] solvers = [ new TriggerSolver(), new ImpulseSolver() ];
	private static Stopwatch watch = Stopwatch.StartNew();
	private static long dynamicsTimestamp = 0, collisionsTimestamp = 0;

	public static void ResolveDynamics(IEnumerable<SceneObject> objects) 
	{
		if (dynamicsTimestamp == 0) 
		{
			dynamicsTimestamp = watch.ElapsedMilliseconds;
			return;
		}

		long current = watch.ElapsedMilliseconds;
		float dt = (float)(current - dynamicsTimestamp) * 1e-3f;
		dynamicsTimestamp = current;

		foreach (var x in objects) 
		{
			var transform = x.Transform;
			var body = x.GetComponent<RigidBody>();

			body.Force += body.Mass * body.Gravity;
			body.Velocity += (body.Force / body.Mass) * dt;
			transform.Translate(body.Velocity * dt);
			body.Force = Vector3.Zero;
		}
	}

	public static void ResolveCollisions(IEnumerable<SceneObject> objects) 
	{
		if (collisionsTimestamp == 0) 
		{
			collisionsTimestamp = watch.ElapsedMilliseconds;
			return;
		}

		long current = watch.ElapsedMilliseconds;
		float dt = (float)(current - collisionsTimestamp) * 1e-3f;
		collisionsTimestamp = current;

		foreach (var a in objects) 
		{
			foreach (var b in objects) 
			{
				if (a == b)
					continue;

				if (Collider.ResolveCollision(source: a, target: b, out Collision collision)) 
				{
					foreach (var x in solvers)
						x.Solve(source: a, target: b, collision, dt);
				}
			}
		}
	}
}

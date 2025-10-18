#pragma warning disable CS0642

using System;

using Vulkan;
using Renderer;

namespace Renderer.Physics;

public abstract class Collider : Component
{
	public bool IsTrigger { set; get; } = false;
	public bool ApplyObjectTransform { set; get; } = true;

	protected Matrix4x4 transform = Matrix4x4.Identity;

	internal static bool ResolveCollision(SceneObject source, SceneObject target, out Collision collision) 
	{
		var a = source.GetComponent<Collider>();
		var b = target.GetComponent<Collider>();

		Matrix4x4 aOriginalTransform = a.transform;
		Matrix4x4 bOriginalTransform = b.transform;

		if (a.ApplyObjectTransform)
			a.transform *= source.Transform;

		if (b.ApplyObjectTransform)
			b.transform *= target.Transform;

		bool hit;

		if (false);
		else if ((a is SphereCollider) && (b is SphereCollider)) hit = CollisionResolver.Resolve((SphereCollider)a, (SphereCollider)b, out collision);
		else if ((a is BoxCollider) && (b is SphereCollider)) hit = CollisionResolver.Resolve((BoxCollider)a, (SphereCollider)b, out collision);
		else if ((a is SphereCollider) && (b is BoxCollider)) hit = CollisionResolver.Resolve((BoxCollider)b, (SphereCollider)a, out collision);
		else if ((a is BoxCollider) && (b is BoxCollider)) hit = CollisionResolver.Resolve((BoxCollider)a, (BoxCollider)b, out collision);
		else throw new InvalidOperationException($"Cannot resolve collision between '{a.GetType()}' and '{b.GetType()}'.");

		a.transform = aOriginalTransform;
		b.transform = bOriginalTransform;

		collision.IsSourceTrigger = a.IsTrigger;
		collision.IsTargetTrigger = b.IsTrigger;

		return hit;
	}
}

public sealed class SphereCollider : Collider
{
	public Vector3 Center 
	{
		set => (transform.tx, transform.ty, transform.tz) = (value.x, value.y, value.z);
		get => transform.t.xyz;
	}

	public float Radius 
	{
		set 
		{
			var x = transform.x.xyz.Normalized * value;
			var y = transform.y.xyz.Normalized * value;
			var z = transform.z.xyz.Normalized * value;

			(transform.xx, transform.xy, transform.xz) = (x.x, x.y, x.z);
			(transform.yx, transform.yy, transform.yz) = (y.x, y.y, y.z);
			(transform.zx, transform.zy, transform.zz) = (z.x, z.y, z.z);
		}
		get => transform.x.xyz.Length;
	}

	public SphereCollider() : base()
	{
		this.Center = Vector3.Zero;
		this.Radius = 1f;
	}
}

public sealed class BoxCollider : Collider
{
	public Vector3 Center 
	{
		set => (transform.tx, transform.ty, transform.tz) = (value.x, value.y, value.z);
		get => transform.t.xyz;
	}

	public Vector3 Size 
	{
		set 
		{
			var x = transform.x.xyz.Normalized * value.x;
			var y = transform.y.xyz.Normalized * value.y;
			var z = transform.z.xyz.Normalized * value.z;

			(transform.xx, transform.xy, transform.xz) = (x.x, x.y, x.z);
			(transform.yx, transform.yy, transform.yz) = (y.x, y.y, y.z);
			(transform.zx, transform.zy, transform.zz) = (z.x, z.y, z.z);
		}
		get => new(transform.x.xyz.Length * 2, transform.y.xyz.Length * 2, transform.z.xyz.Length * 2);
	}

	public Quaternion Rotation 
	{
		set => transform *= Matrix4x4.Rotate(value);
		get => throw new NotImplementedException();
	}

	public BoxCollider() : base()
	{
		this.Center = Vector3.Zero;
		this.Size = Vector3.One;
		this.Rotation = Quaternion.Identity;
	}
}

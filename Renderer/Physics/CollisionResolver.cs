using System;

using Vulkan;
using Renderer;

namespace Renderer.Physics;

internal static class CollisionResolver 
{
	public static bool Resolve(SphereCollider a, SphereCollider b, out Collision collision) 
	{
		var centerA = a.Center;
		var centerB = b.Center;

		if (centerA == centerB) 
		{
			collision = new Collision { Normal = Vector3.Zero };
			return true;
		}

		Vector3 normal = centerB - centerA;
		float distance = normal.Length;

		var radiusA = a.Radius;
		var radiusB = b.Radius;

		bool hit = distance <= radiusA + radiusB;

		collision = new Collision { Normal = normal * ((radiusA + radiusB - distance) / distance) };
		return hit;
	}

	public static bool Resolve(BoxCollider a, BoxCollider b, out Collision collision) 
	{
		var aCenter = a.Center;
		var aSize = a.Size;
		var bCenter = b.Center;
		var bSize = b.Size;

		Vector3 normal = (bCenter - aCenter).Normalized;
        
        // Calculate half extents for both boxes
        Vector3 halfExtentsA = aSize * 0.5f;
        Vector3 halfExtentsB = bSize * 0.5f;
        
        // Calculate the distance between centers
        Vector3 distance = bCenter - aCenter;
        
        // Calculate overlap on each axis
        float overlapX = (halfExtentsA.x + halfExtentsB.x) - Math.Abs(distance.x);
        float overlapY = (halfExtentsA.y + halfExtentsB.y) - Math.Abs(distance.y);
        float overlapZ = (halfExtentsA.z + halfExtentsB.z) - Math.Abs(distance.z);
        
        // Check if there's no collision (separation on any axis means no collision)
        if (overlapX <= 0 || overlapY <= 0 || overlapZ <= 0)
        {
        	collision = new Collision { Normal = normal };
            return false;
        }
        
        // Find the axis with minimum overlap (minimum translation vector)
        float minOverlap = Math.Min(overlapX, Math.Min(overlapY, overlapZ));
        
        // Determine the separation direction and calculate normal
        if (minOverlap == overlapX)
        {
            // Separate along x axis
            float direction = distance.x >= 0 ? 1 : -1;
            normal = new Vector3(direction * overlapX, 0, 0);
        }
        else if (minOverlap == overlapY)
        {
            // Separate along y axis
            float direction = distance.y >= 0 ? 1 : -1;
            normal = new Vector3(0, direction * overlapY, 0);
        }
        else
        {
            // Separate along z axis
            float direction = distance.z >= 0 ? 1 : -1;
            normal = new Vector3(0, 0, direction * overlapZ);
        }
        
        collision = new Collision { Normal = normal };
        return true;
	}

	public static bool Resolve(BoxCollider a, SphereCollider b, out Collision collision) 
	{
		var aCenter = a.Center;
		var aSize = a.Size;
		var bCenter = b.Center;
		var bRadius = b.Radius;

		Vector3 normal = (bCenter - aCenter).Normalized;

		// Calculate half extents of the box (from center to edge)
		Vector3 halfExtents = aSize * 0.5f;
		
		// Find the closest point on the box to the sphere's center
		Vector3 sphereToBox = bCenter - aCenter;
		
		// Clamp the sphere's center to the box bounds to find closest point
		Vector3 closestPoint = Vector3.Zero;
		closestPoint.x = Math.Max(-halfExtents.x, Math.Min(sphereToBox.x, halfExtents.x));
		closestPoint.y = Math.Max(-halfExtents.y, Math.Min(sphereToBox.y, halfExtents.y));
		closestPoint.z = Math.Max(-halfExtents.z, Math.Min(sphereToBox.z, halfExtents.z));
		
		// Convert back to world space
		closestPoint += aCenter;
		
		// Calculate distance from sphere center to closest point
		Vector3 distance = bCenter - closestPoint;
		float distanceSquared = distance.x * distance.x + distance.y * distance.y + distance.z * distance.z;
		
		// Check if collision occurs
		if (distanceSquared <= bRadius * bRadius) 
		{
			float distanceLength = (float)Math.Sqrt(distanceSquared);
			
			// Handle special case where sphere center is exactly on or inside the box
			if (distanceLength == 0) 
			{
				// Find the axis with minimum penetration
				Vector3 penetration = Vector3.Zero;
				float minPenetration = float.MaxValue;
				
				// Check x axis
				float xPenetration = halfExtents.x - Math.Abs(sphereToBox.x);
				if (xPenetration < minPenetration) 
				{
					minPenetration = xPenetration;
					penetration = new Vector3(sphereToBox.x >= 0 ? 1 : -1, 0, 0);
				}
				
				// Check y axis
				float yPenetration = halfExtents.y - Math.Abs(sphereToBox.y);
				if (yPenetration < minPenetration) 
				{
					minPenetration = yPenetration;
					penetration = new Vector3(0, sphereToBox.y >= 0 ? 1 : -1, 0);
				}
				
				// Check z axis
				float zPenetration = halfExtents.z - Math.Abs(sphereToBox.z);
				if (zPenetration < minPenetration) 
				{
					minPenetration = zPenetration;
					penetration = new Vector3(0, 0, sphereToBox.z >= 0 ? 1 : -1);
				}
				
				// Calculate minimum separation distance
				normal = penetration * (minPenetration + bRadius);
			}
			else
			{
				// Normal case: sphere center is outside box but within radius
				Vector3 normalizedDistance = distance / distanceLength;
				float penetrationDepth = bRadius - distanceLength;
				normal = normalizedDistance * penetrationDepth;
			}
			
			collision = new Collision { Normal = normal };
			return true;
		}
		
		collision = new Collision { Normal = normal };
		return false;
	}
}

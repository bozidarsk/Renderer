using Vulkan;

namespace Renderer;

public record struct GlobalUniforms(
	Matrix4x4 View,
	Matrix4x4 Projection,
	Vector3 CameraPosition
);

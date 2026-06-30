namespace Renderer;

internal record struct PushConstants(
	Matrix4x4 Model,
	uint Id
);

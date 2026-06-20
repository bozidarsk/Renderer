namespace Renderer;

public record struct PushConstants(
	Matrix4x4 Model,
	uint Id
);

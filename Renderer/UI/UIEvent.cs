using System;

namespace Renderer.UI;

internal record UIEvent(EventType Type, EventPropagationType Propagation, object? Sender, EventArgs Args, uint Target);

using System;
using System.Threading;

namespace Renderer;

public abstract class Asset : IDisposable
{
	private protected readonly Renderer renderer = Renderer.Require();

	private bool disposed = false;

	public void Dispose()
	{
		if (Interlocked.Exchange(ref disposed, true))
			return;

		Free();
		GC.SuppressFinalize(this);
	}

	protected abstract void Free();

	protected Asset() => renderer.Assets.Add(new WeakReference(this, trackResurrection: true));

	~Asset()
	{
		if (Interlocked.Exchange(ref disposed, true))
	        return;

		Free();
	}
}

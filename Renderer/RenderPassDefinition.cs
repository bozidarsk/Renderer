using System;
using System.IO;

namespace Renderer;

public class RenderPassDefinition
{
	public string File { get; }

	public RenderPassDefinition(string filename)
	{
		if (filename == null)
			throw new ArgumentNullException();

		if (!System.IO.File.Exists(filename))
			throw new FileNotFoundException();

		this.File = Path.GetFullPath(filename);
	}
}

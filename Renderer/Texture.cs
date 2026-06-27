using System;
using System.IO;

using Vulkan;

namespace Renderer;

public class Texture
{
	public Array? Data { get; }
	public int Width { get; }
	public int Height { get; }
	public Format Format { get; }
	public ImageType Type { get; }
	public ImageUsage Usage { get; }
	public ImageAspect Aspect { get; }

	private const ImageType DEFAULT_TYPE = ImageType.Generic2D;
	private const ImageUsage DEFAULT_USAGE = ImageUsage.TransferDst | ImageUsage.Sampled;
	private const ImageAspect DEFAULT_ASPECT = ImageAspect.Color;

	public Texture(int width, int height, Format format, ImageType type, ImageUsage usage, ImageAspect aspect)
	{
		if (width <= 0 || height <= 0)
			throw new ArgumentOutOfRangeException();

		this.Width = width;
		this.Height = height;
		this.Format = format;
		this.Type = type;
		this.Usage = usage;
		this.Aspect = aspect;
	}

	public Texture(int width, int height, uint[] data, ImageType type = DEFAULT_TYPE, ImageUsage usage = DEFAULT_USAGE, ImageAspect aspect = DEFAULT_ASPECT) :
		this(width, height, format: Format.B8G8R8A8UNorm, type: type, usage: usage, aspect: aspect)
	{
		this.Data = data;
	}

	public Texture(int width, int height, Color[] data, ImageType type = DEFAULT_TYPE, ImageUsage usage = DEFAULT_USAGE, ImageAspect aspect = DEFAULT_ASPECT) :
		this(width, height, format: Format.R32G32B32A32SFloat, type: type, usage: usage, aspect: aspect)
	{
		this.Data = data;
	}

	public Texture(string filename, ImageType type = DEFAULT_TYPE, ImageUsage usage = DEFAULT_USAGE, ImageAspect aspect = DEFAULT_ASPECT)
	{
		if (filename == null)
			throw new ArgumentNullException();

		this.Type = type;
		this.Usage = usage;
		this.Aspect = aspect;

		var extension = Path.GetExtension(filename).ToLower();
		switch (extension)
		{
			case ".png":
				var png = PNG.FromFile(filename);
				this.Width = png.Width;
				this.Height = png.Height;
				this.Format = Format.B8G8R8A8UNorm;
				this.Data = png.Colors;
				break;
			default:
				throw new InvalidOperationException($"Failed to parse texture of type '{extension}'.");
		}
	}
}

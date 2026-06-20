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

	private const Format DEFAULT_FORMAT = Format.B8G8R8A8UNorm;
	private const ImageType DEFAULT_TYPE = ImageType.Generic2D;
	private const ImageUsage DEFAULT_USAGE = ImageUsage.TransferDst | ImageUsage.Sampled;
	private const ImageAspect DEFAULT_ASPECT = ImageAspect.Color;

	public Texture(string filename) : this(1, 1, format: Format.B8G8R8A8UNorm, type: ImageType.Generic2D)
	{
		if (filename == null)
			throw new ArgumentNullException();

		var extension = Path.GetExtension(filename).ToLower();
		switch (extension)
		{
			case ".png":
				var png = PNG.FromFile(filename);
				this.Width = png.Width;
				this.Height = png.Height;
				this.Data = png.Colors;
				// TODO: set format according to color data
				break;
			default:
				throw new InvalidOperationException($"Failed to parse texture of type '{extension}'.");
		}
	}

	public Texture(int width, int height, Format format = DEFAULT_FORMAT, ImageType type = DEFAULT_TYPE, ImageUsage usage = DEFAULT_USAGE, ImageAspect aspect = DEFAULT_ASPECT)
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

	public Texture(int width, int height, Color[] data, ImageType type = DEFAULT_TYPE, ImageUsage usage = DEFAULT_USAGE, ImageAspect aspect = DEFAULT_ASPECT)
	{
		if (width <= 0 || height <= 0)
			throw new ArgumentOutOfRangeException();

		if (width * height != data.Length)
			throw new ArgumentException();

		this.Width = width;
		this.Height = height;
		this.Format = Format.R32G32B32A32SFloat;
		this.Type = type;
		this.Usage = usage;
		this.Aspect = aspect;

		this.Data = data;
	}
}

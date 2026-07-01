using System;
using System.IO;

using Vulkan;

namespace Renderer;

public class Texture
{
	public Array? Data { get; }
	public int Width { get; }
	public int Height { get; }
	public int Depth { get; }
	public Format Format { get; }
	public ImageType Type { get; }
	public ImageUsage Usage { get; }
	public ImageAspect Aspect { get; }
	public ImageLayout InitialLayout { get; }

	public Texture(int width, int height, Format format, ImageUsage usage, ImageAspect aspect, ImageLayout initialLayout)
	{
		if (width <= 0 || height <= 0)
			throw new ArgumentOutOfRangeException();

		this.Width = width;
		this.Height = height;
		this.Depth = 1;
		this.Format = format;
		this.Type = ImageType.Generic2D;
		this.Usage = usage;
		this.Aspect = aspect;
		this.InitialLayout = initialLayout;
	}

	public Texture(int width, int height, uint[] data)
	{
		if (width <= 0 || height <= 0 || width * height != data.Length)
			throw new ArgumentOutOfRangeException();

		this.Data = data;
		this.Width = width;
		this.Height = height;
		this.Depth = 1;
		this.Format = Format.B8G8R8A8UNorm;
		this.Type = ImageType.Generic2D;
		this.Usage = ImageUsage.TransferDst | ImageUsage.Sampled;
		this.Aspect = ImageAspect.Color;
		this.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;
	}

	public Texture(int width, int height, Color[] data)
	{
		if (width <= 0 || height <= 0 || width * height != data.Length)
			throw new ArgumentOutOfRangeException();

		this.Data = data;
		this.Width = width;
		this.Height = height;
		this.Depth = 1;
		this.Format = Format.R32G32B32A32SFloat;
		this.Type = ImageType.Generic2D;
		this.Usage = ImageUsage.TransferDst | ImageUsage.Sampled;
		this.Aspect = ImageAspect.Color;
		this.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;
	}

	public Texture(int width, int height, int depth, Format format, ImageUsage usage, ImageAspect aspect, ImageLayout initialLayout)
	{
		if (width <= 0 || height <= 0 || depth <= 0)
			throw new ArgumentOutOfRangeException();

		this.Width = width;
		this.Height = height;
		this.Depth = depth;
		this.Format = format;
		this.Type = ImageType.Generic2D;
		this.Usage = usage;
		this.Aspect = aspect;
		this.InitialLayout = initialLayout;
	}

	public Texture(int width, int height, int depth, uint[] data)
	{
		if (width <= 0 || height <= 0 || depth <= 0 || width * height * depth != data.Length)
			throw new ArgumentOutOfRangeException();

		this.Data = data;
		this.Width = width;
		this.Height = height;
		this.Depth = depth;
		this.Format = Format.B8G8R8A8UNorm;
		this.Type = ImageType.Generic2D;
		this.Usage = ImageUsage.TransferDst | ImageUsage.Sampled;
		this.Aspect = ImageAspect.Color;
		this.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;
	}

	public Texture(int width, int height, int depth, Color[] data)
	{
		if (width <= 0 || height <= 0 || depth <= 0 || width * height * depth != data.Length)
			throw new ArgumentOutOfRangeException();

		this.Data = data;
		this.Width = width;
		this.Height = height;
		this.Depth = depth;
		this.Format = Format.R32G32B32A32SFloat;
		this.Type = ImageType.Generic2D;
		this.Usage = ImageUsage.TransferDst | ImageUsage.Sampled;
		this.Aspect = ImageAspect.Color;
		this.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;
	}

	public Texture(string filename)
	{
		if (filename == null)
			throw new ArgumentNullException();

		this.Usage = ImageUsage.TransferDst | ImageUsage.Sampled;
		this.Aspect = ImageAspect.Color;
		this.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;

		var extension = Path.GetExtension(filename).ToLower();
		switch (extension)
		{
			case ".png":
				var png = PNG.FromFile(filename);
				this.Width = png.Width;
				this.Height = png.Height;
				this.Depth = 1;
				this.Format = Format.B8G8R8A8UNorm;
				this.Type = ImageType.Generic2D;
				this.Data = png.Colors;
				break;
			default:
				throw new InvalidOperationException($"Failed to parse texture of type '{extension}'.");
		}
	}
}

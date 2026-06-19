using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Vulkan;

namespace Renderer;

public class Texture : IDisposable
{
	public Image Image { get; }
	public ImageView ImageView { get; }
	public DeviceMemory ImageMemory { get; }
	public Sampler Sampler { get; }

	public Extent2D Extent { get; }
	public Format Format { get; }
	public ImageType Type { get; }
	public ImageUsage Usage { get; }
	public ImageAspect Aspect { get; }

	public int Width => (int)this.Extent.Width;
	public int Height => (int)this.Extent.Height;

	private const Format DEFAULT_FORMAT = Format.B8G8R8A8UNorm;
	private const ImageType DEFAULT_TYPE = ImageType.Generic2D;
	private const ImageUsage DEFAULT_USAGE = ImageUsage.TransferDst | ImageUsage.Sampled;
	private const ImageAspect DEFAULT_ASPECT = ImageAspect.Color;

	public virtual void Dispose()
	{
		Image.Dispose();
		ImageView.Dispose();
		ImageMemory?.Dispose();
		Sampler.Dispose();
	}

#pragma warning disable CS8618
	private Texture(Extent2D extent, Format format, ImageType type, ImageUsage usage = DEFAULT_USAGE, ImageAspect aspect = DEFAULT_ASPECT)
	{
		this.Extent = extent;
		this.Format = format;
		this.Type = type;
		this.Usage = usage;
		this.Aspect = aspect;
	}

	private Texture(int width, int height, Format format, ImageType type, ImageUsage usage = DEFAULT_USAGE, ImageAspect aspect = DEFAULT_ASPECT)
	{
		if (width < 0 || height < 0)
			throw new ArgumentOutOfRangeException();

		this.Extent = new((uint)width, (uint)height);
		this.Format = format;
		this.Type = type;
		this.Usage = usage;
		this.Aspect = aspect;
	}
#pragma warning restore

	public Texture(Renderer renderer, string filename) : this(0, 0, Format.B8G8R8A8UNorm, ImageType.Generic2D)
	{
		if (filename == null)
			throw new ArgumentNullException();

		Array data;

		var extension = Path.GetExtension(filename).ToLower();
		switch (extension)
		{
			case ".png":
				var png = PNG.FromFile(filename);
				this.Extent = new((uint)png.Width, (uint)png.Height);
				data = png.Colors;
				break;
			default:
				throw new InvalidOperationException($"Failed to parse texture of type '{extension}'.");
		}

		renderer.CreateTexture(
			ref MemoryMarshal.GetArrayDataReference(data),
			this.Width,
			this.Height,
			this.Type,
			this.Format,
			out Image image,
			out ImageView imageView,
			out DeviceMemory imageMemory,
			out Sampler sampler
		);

		this.Image = image;
		this.ImageView = imageView;
		this.ImageMemory = imageMemory;
		this.Sampler = sampler;
	}

	public Texture(Renderer renderer, Extent2D extent, ref byte data, Format format = DEFAULT_FORMAT, ImageType type = DEFAULT_TYPE) : this(extent, format, type)
	{
		renderer.CreateTexture(
			ref data,
			this.Width,
			this.Height,
			this.Type,
			this.Format,
			out Image image,
			out ImageView imageView,
			out DeviceMemory imageMemory,
			out Sampler sampler
		);

		this.Image = image;
		this.ImageView = imageView;
		this.ImageMemory = imageMemory;
		this.Sampler = sampler;
	}

	public Texture(Renderer renderer, Extent2D extent, ref Color data, Format format = DEFAULT_FORMAT, ImageType type = DEFAULT_TYPE) : this(extent, format, type)
	{
		renderer.CreateTexture(
			ref Unsafe.As<Color, byte>(ref data),
			this.Width,
			this.Height,
			this.Type,
			this.Format,
			out Image image,
			out ImageView imageView,
			out DeviceMemory imageMemory,
			out Sampler sampler
		);

		this.Image = image;
		this.ImageView = imageView;
		this.ImageMemory = imageMemory;
		this.Sampler = sampler;
	}

	public Texture(Renderer renderer, int width, int height, ref byte data, Format format = DEFAULT_FORMAT, ImageType type = DEFAULT_TYPE) : this(width, height, format, type)
	{
		renderer.CreateTexture(
			ref data,
			this.Width,
			this.Height,
			this.Type,
			this.Format,
			out Image image,
			out ImageView imageView,
			out DeviceMemory imageMemory,
			out Sampler sampler
		);

		this.Image = image;
		this.ImageView = imageView;
		this.ImageMemory = imageMemory;
		this.Sampler = sampler;
	}

	public Texture(Renderer renderer, int width, int height, ref Color data, Format format = DEFAULT_FORMAT, ImageType type = DEFAULT_TYPE) : this(width, height, format, type)
	{
		renderer.CreateTexture(
			ref Unsafe.As<Color, byte>(ref data),
			this.Width,
			this.Height,
			this.Type,
			this.Format,
			out Image image,
			out ImageView imageView,
			out DeviceMemory imageMemory,
			out Sampler sampler
		);

		this.Image = image;
		this.ImageView = imageView;
		this.ImageMemory = imageMemory;
		this.Sampler = sampler;
	}

	public Texture(Renderer renderer, Extent2D extent, ImageUsage usage, ImageAspect aspect = DEFAULT_ASPECT, Format format = DEFAULT_FORMAT, ImageType type = DEFAULT_TYPE) : this(extent, format, type, usage, aspect)
	{
		renderer.CreateImage(this.Width, this.Height, type, usage, this.Format, out Image image);
		renderer.CreateImageMemory(image, out DeviceMemory imageMemory);
		renderer.CreateImageView(image, this.Format, aspect, out ImageView imageView);
		renderer.CreateSampler(out Sampler sampler);

		this.Image = image;
		this.ImageView = imageView;
		this.ImageMemory = imageMemory;
		this.Sampler = sampler;
	}

	public Texture(Renderer renderer, int width, int height, ImageUsage usage, ImageAspect aspect = DEFAULT_ASPECT, Format format = DEFAULT_FORMAT, ImageType type = DEFAULT_TYPE) : this(width, height, format, type, usage, aspect)
	{
		renderer.CreateImage(this.Width, this.Height, type, usage, this.Format, out Image image);
		renderer.CreateImageMemory(image, out DeviceMemory imageMemory);
		renderer.CreateImageView(image, this.Format, aspect, out ImageView imageView);
		renderer.CreateSampler(out Sampler sampler);

		this.Image = image;
		this.ImageView = imageView;
		this.ImageMemory = imageMemory;
		this.Sampler = sampler;
	}
}

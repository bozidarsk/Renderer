using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Vulkan;

namespace Renderer;

public sealed class Texture : IDisposable
{
	public readonly Image Image;
	public readonly ImageView ImageView;
	public readonly DeviceMemory? ImageMemory = null;
	public readonly Sampler Sampler;

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

	public void Dispose() 
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

	public Texture(Vulkan.Program vk, string filename) : this(0, 0, Format.B8G8R8A8UNorm, ImageType.Generic2D)
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

		vk.CreateTexture(
			ref MemoryMarshal.GetArrayDataReference(data),
			this.Width,
			this.Height,
			this.Type,
			this.Format,
			out this.Image,
			out this.ImageView,
			out this.ImageMemory,
			out this.Sampler
		);
	}

	public Texture(Program vk, Extent2D extent, ref byte data, Format format = DEFAULT_FORMAT, ImageType type = DEFAULT_TYPE) : this(extent, format, type)
	{
		vk.CreateTexture(
			ref data,
			this.Width,
			this.Height,
			this.Type,
			this.Format,
			out this.Image,
			out this.ImageView,
			out this.ImageMemory,
			out this.Sampler
		);
	}

	public Texture(Program vk, Extent2D extent, ref Color data, Format format = DEFAULT_FORMAT, ImageType type = DEFAULT_TYPE) : this(extent, format, type)
	{
		vk.CreateTexture(
			ref Unsafe.As<Color, byte>(ref data),
			this.Width,
			this.Height,
			this.Type,
			this.Format,
			out this.Image,
			out this.ImageView,
			out this.ImageMemory,
			out this.Sampler
		);
	}

	public Texture(Program vk, int width, int height, ref byte data, Format format = DEFAULT_FORMAT, ImageType type = DEFAULT_TYPE) : this(width, height, format, type)
	{
		vk.CreateTexture(
			ref data,
			this.Width,
			this.Height,
			this.Type,
			this.Format,
			out this.Image,
			out this.ImageView,
			out this.ImageMemory,
			out this.Sampler
		);
	}

	public Texture(Program vk, int width, int height, ref Color data, Format format = DEFAULT_FORMAT, ImageType type = DEFAULT_TYPE) : this(width, height, format, type)
	{
		vk.CreateTexture(
			ref Unsafe.As<Color, byte>(ref data),
			this.Width,
			this.Height,
			this.Type,
			this.Format,
			out this.Image,
			out this.ImageView,
			out this.ImageMemory,
			out this.Sampler
		);
	}

	public Texture(Program vk, Extent2D extent, ImageUsage usage, ImageAspect aspect = DEFAULT_ASPECT, Format format = DEFAULT_FORMAT, ImageType type = DEFAULT_TYPE) : this(extent, format, type, usage, aspect)
	{
		vk.CreateImage(this.Width, this.Height, type, usage, this.Format, out this.Image);
		vk.CreateImageMemory(this.Image, out this.ImageMemory);
		vk.CreateImageView(this.Image, this.Format, aspect, out this.ImageView);
		vk.CreateSampler(out this.Sampler);
	}

	public Texture(Program vk, int width, int height, ImageUsage usage, ImageAspect aspect = DEFAULT_ASPECT, Format format = DEFAULT_FORMAT, ImageType type = DEFAULT_TYPE) : this(width, height, format, type, usage, aspect)
	{
		vk.CreateImage(this.Width, this.Height, type, usage, this.Format, out this.Image);
		vk.CreateImageMemory(this.Image, out this.ImageMemory);
		vk.CreateImageView(this.Image, this.Format, aspect, out this.ImageView);
		vk.CreateSampler(out this.Sampler);
	}
}

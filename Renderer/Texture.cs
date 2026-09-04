using System;
using System.IO;

using Vulkan;

namespace Renderer;

public class Texture : Asset
{
	public int Width { get; }
	public int Height { get; }
	public int Depth { get; }
	public Format Format { get; }
	public ImageType Type { get; }
	public ImageUsage Usage { get; }
	public ImageAspect Aspect { get; }
	public ImageLayout InitialLayout { get; }

	internal Image Image { private set; get; }
	internal ImageView ImageView { private set; get; }
	internal DeviceMemory ImageMemory { private set; get; }
	internal Sampler Sampler { private set; get; }

	private void Initialize(Array? data)
	{
		renderer.CreateImage(this.Width, this.Height, this.Type, this.Usage, this.Format, out var image);
		renderer.CreateImageMemory(image, out var imageMemory);

		if (data != null)
		{
			renderer.CreateStagingBuffer(data, BufferUsage.TransferSrc, out var buffer, out var memory);

			renderer.TransitionImageLayout(image, ImageLayout.Undefined, ImageLayout.TransferDstOptimal, this.Aspect);
			renderer.CopyBufferToImage(buffer, image, this.Width, this.Height, this.Aspect);
			renderer.TransitionImageLayout(image, ImageLayout.TransferDstOptimal, this.InitialLayout, this.Aspect);

			memory.Dispose();
			buffer.Dispose();
		}
		else
		{
			renderer.TransitionImageLayout(image, ImageLayout.Undefined, this.InitialLayout, this.Aspect);
		}

		renderer.CreateImageView(image, this.Format, this.Aspect, this.Type switch
		{
			ImageType.Generic1D => ImageViewType.Generic1D,
			ImageType.Generic2D => ImageViewType.Generic2D,
			ImageType.Generic3D => ImageViewType.Generic3D,
			_ => throw new InvalidOperationException($"Cannot map ImageType.{this.Type} to ImageViewType.")
		}, out var imageView);

		renderer.CreateSampler(out var sampler, ((renderer.PhysicalDevice.GetFormatProperties(this.Format).OptimalTilingFeatures & FormatFeatures.SampledImageFilterLinear) != 0) ? Filter.Linear : Filter.Nearest);

		this.Image = image;
		this.ImageView = imageView;
		this.ImageMemory = imageMemory;
		this.Sampler = sampler;
	}

	protected override void Free()
	{
		renderer.ToBeDisposed(Sampler);
		renderer.ToBeDisposed(ImageView);
		renderer.ToBeDisposed(Image);
		renderer.ToBeDisposed(ImageMemory);
	}

#pragma warning disable CS8618

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

		Initialize(null);
	}

	public Texture(int width, int height, uint[] data)
	{
		if (width <= 0 || height <= 0 || width * height != data.Length)
			throw new ArgumentOutOfRangeException();

		this.Width = width;
		this.Height = height;
		this.Depth = 1;
		this.Format = Format.B8G8R8A8UNorm;
		this.Type = ImageType.Generic2D;
		this.Usage = ImageUsage.TransferDst | ImageUsage.Sampled;
		this.Aspect = ImageAspect.Color;
		this.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;

		Initialize(data);
	}

	public Texture(int width, int height, Color[] data)
	{
		if (width <= 0 || height <= 0 || width * height != data.Length)
			throw new ArgumentOutOfRangeException();

		this.Width = width;
		this.Height = height;
		this.Depth = 1;
		this.Format = Format.R32G32B32A32SFloat;
		this.Type = ImageType.Generic2D;
		this.Usage = ImageUsage.TransferDst | ImageUsage.Sampled;
		this.Aspect = ImageAspect.Color;
		this.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;

		Initialize(data);
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

		Initialize(null);
	}

	public Texture(int width, int height, int depth, uint[] data)
	{
		if (width <= 0 || height <= 0 || depth <= 0 || width * height * depth != data.Length)
			throw new ArgumentOutOfRangeException();

		this.Width = width;
		this.Height = height;
		this.Depth = depth;
		this.Format = Format.B8G8R8A8UNorm;
		this.Type = ImageType.Generic2D;
		this.Usage = ImageUsage.TransferDst | ImageUsage.Sampled;
		this.Aspect = ImageAspect.Color;
		this.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;

		Initialize(data);
	}

	public Texture(int width, int height, int depth, Color[] data)
	{
		if (width <= 0 || height <= 0 || depth <= 0 || width * height * depth != data.Length)
			throw new ArgumentOutOfRangeException();

		this.Width = width;
		this.Height = height;
		this.Depth = depth;
		this.Format = Format.R32G32B32A32SFloat;
		this.Type = ImageType.Generic2D;
		this.Usage = ImageUsage.TransferDst | ImageUsage.Sampled;
		this.Aspect = ImageAspect.Color;
		this.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;

		Initialize(data);
	}

	public Texture(string filename)
	{
		if (filename == null)
			throw new ArgumentNullException();

		this.Usage = ImageUsage.TransferDst | ImageUsage.Sampled;
		this.Aspect = ImageAspect.Color;
		this.InitialLayout = ImageLayout.ShaderReadOnlyOptimal;

		Array data;

		var extension = Path.GetExtension(filename).ToLower();
		switch (extension)
		{
			case ".png":
				var png = PNG.FromFile(filename);
				data = png.Colors;
				this.Width = png.Width;
				this.Height = png.Height;
				this.Depth = 1;
				this.Format = Format.B8G8R8A8UNorm;
				this.Type = ImageType.Generic2D;
				break;
			default:
				throw new InvalidOperationException($"Failed to parse texture of type '{extension}'.");
		}

		Initialize(data);
	}

#pragma warning restore
}

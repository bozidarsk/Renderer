using System;
using System.IO;
using System.Threading.Tasks;

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

	internal Image Image { private set; get; }
	internal ImageView ImageView { private set; get; }
	internal DeviceMemory ImageMemory { private set; get; }
	internal Sampler Sampler { private set; get; }

	protected override void Free()
	{
		renderer.ToBeDisposed(Sampler);
		renderer.ToBeDisposed(ImageView);
		renderer.ToBeDisposed(Image);
		renderer.ToBeDisposed(ImageMemory);
	}

	public async Task CopyToAsync(Buffer destination, ulong? bufferOffset = null, Offset3D? imageOffset = null, Extent3D? imageExtent = null)
	{
		if (destination == null)
			throw new ArgumentNullException();

		var region = new BufferImageCopy(
			bufferOffset: bufferOffset ?? 0,
			bufferRowLength: 0,
			bufferImageHeight: 0,
			imageSubresource: new(
				aspect: this.Aspect,
				mipLevel: 0,
				baseArrayLayer: 0,
				layerCount: 1
			),
			imageOffset: imageOffset ?? new(x: 0, y: 0, z: 0),
			imageExtent: imageExtent ?? new(width: (uint)this.Width, height: (uint)this.Height, depth: (uint)this.Depth)
		);

		await renderer.TransferQueueContext.SubmitAsync(cmd => cmd.CopyImageToBuffer(this.Image, destination.VkBuffer, ImageLayout.TransferSrcOptimal, region));
	}

	public async Task TransitionLayout(ImageLayout from, ImageLayout to)
	{
		Access sourceAccess, destinationAccess;
		PipelineStage sourceStage, destinationStage;

		if (from == ImageLayout.Undefined && to == ImageLayout.TransferDstOptimal)
		{
			sourceAccess = 0;
			destinationAccess = Access.TransferWrite;

			sourceStage = PipelineStage.TopOfPipe;
			destinationStage = PipelineStage.Transfer;
		}
		else if (from == ImageLayout.TransferDstOptimal && to == ImageLayout.ShaderReadOnlyOptimal)
		{
			sourceAccess = Access.TransferWrite;
			destinationAccess = Access.ShaderRead;

			sourceStage = PipelineStage.Transfer;
			destinationStage = PipelineStage.FragmentShader;
		}
		else if (from == ImageLayout.Undefined && to == ImageLayout.ColorAttachmentOptimal)
		{
			sourceAccess = 0;
			destinationAccess = Access.ColorAttachmentWrite;

			sourceStage = PipelineStage.TopOfPipe;
			destinationStage = PipelineStage.ColorAttachmentOutput;
		}
		else if (from == ImageLayout.Undefined && to == ImageLayout.ShaderReadOnlyOptimal)
		{
			sourceAccess = 0;
			destinationAccess = Access.ShaderRead;

			sourceStage = PipelineStage.TopOfPipe;
			destinationStage = PipelineStage.FragmentShader;
		}
		else if (from == ImageLayout.ColorAttachmentOptimal && to == ImageLayout.ShaderReadOnlyOptimal)
		{
			sourceAccess = Access.ColorAttachmentWrite;
			destinationAccess = Access.ShaderRead;

			sourceStage = PipelineStage.ColorAttachmentOutput;
			destinationStage = PipelineStage.FragmentShader;
		}
		else if (from == ImageLayout.ShaderReadOnlyOptimal && to == ImageLayout.ColorAttachmentOptimal)
		{
			sourceAccess = Access.ShaderRead;
			destinationAccess = Access.ColorAttachmentWrite;

			sourceStage = PipelineStage.FragmentShader;
			destinationStage = PipelineStage.ColorAttachmentOutput;
		}
		else if (from == ImageLayout.PresentSrc && to == ImageLayout.ShaderReadOnlyOptimal)
		{
			sourceAccess = Access.None;
			destinationAccess = Access.ShaderRead;

			sourceStage = PipelineStage.BottomOfPipe;
			destinationStage = PipelineStage.FragmentShader;
		}
		else if (from == ImageLayout.PresentSrc && to == ImageLayout.ColorAttachmentOptimal)
		{
			sourceAccess = Access.None;
			destinationAccess = Access.ColorAttachmentWrite;

			sourceStage = PipelineStage.BottomOfPipe;
			destinationStage = PipelineStage.ColorAttachmentOutput;
		}
		else if (from == ImageLayout.TransferSrcOptimal && to == ImageLayout.ShaderReadOnlyOptimal)
		{
			sourceAccess = Access.TransferRead;
			destinationAccess = Access.ShaderRead;

			sourceStage = PipelineStage.Transfer;
			destinationStage = PipelineStage.FragmentShader;
		}
		else if (from == ImageLayout.ShaderReadOnlyOptimal && to == ImageLayout.TransferSrcOptimal)
		{
			sourceAccess = Access.ShaderRead;
			destinationAccess = Access.TransferRead;

			sourceStage = PipelineStage.FragmentShader;
			destinationStage = PipelineStage.Transfer;
		}
		else if (from == ImageLayout.ColorAttachmentOptimal && to == ImageLayout.TransferSrcOptimal)
		{
			sourceAccess = Access.ColorAttachmentWrite;
			destinationAccess = Access.TransferRead;

			sourceStage = PipelineStage.ColorAttachmentOutput;
			destinationStage = PipelineStage.Transfer;
		}
		else if (from == ImageLayout.Undefined && to == ImageLayout.DepthAttachmentOptimal)
		{
			sourceAccess = 0;
			destinationAccess = Access.DepthStencilAttachmentRead | Access.DepthStencilAttachmentWrite;

			sourceStage = PipelineStage.TopOfPipe;
			destinationStage = PipelineStage.EarlyFragmentTests;
		}
		else if (from == ImageLayout.Undefined && to == ImageLayout.TransferSrcOptimal)
		{
			sourceAccess = 0;
			destinationAccess = Access.TransferRead;

			sourceStage = PipelineStage.TopOfPipe;
			destinationStage = PipelineStage.Transfer;
		}
		else
			throw new InvalidOperationException($"Unsupported layer transition from '{from}' to '{to}'.");

		var barrier = new ImageMemoryBarrier(
			next: default,
			srcAccess: sourceAccess,
			dstAccess: destinationAccess,
			oldLayout: from,
			newLayout: to,
			srcQueueFamilyIndex: ~0u,
			dstQueueFamilyIndex: ~0u,
			image: Image,
			subresourceRange: new(
				aspect: Aspect,
				baseMipLevel: 0,
				levelCount: 1,
				baseArrayLayer: 0,
				layerCount: 1
			)
		);

		await renderer.TransferQueueContext.SubmitAsync(cmd =>
			cmd.PipelineBarrier(
				srcStage: sourceStage,
				dstStage: destinationStage,
				dependencyFlags: default,
				memoryBarriers: null,
				bufferMemoryBarriers: null,
				imageMemoryBarriers: [barrier]
			)
		);
	}

	public static Texture Create(int width, int height, Format format, ImageUsage usage, ImageAspect aspect) => new(width, height, 1, format, ImageType.Generic2D, usage, aspect);
	public static Texture Create(int width, int height, int depth, Format format, ImageUsage usage, ImageAspect aspect) => new(width, height, depth, format, ImageType.Generic3D, usage, aspect);

	public static async Task<Texture> CreateAsync(int width, int height, uint[] data)
	{
		var texture = new Texture(width, height, 1, Format.B8G8R8A8UNorm, ImageType.Generic2D, ImageUsage.TransferDst | ImageUsage.Sampled, ImageAspect.Color);
		await texture.TransitionLayout(ImageLayout.Undefined, ImageLayout.TransferDstOptimal);

		using var buffer = await Buffer.CreateAsync(data, BufferUsage.TransferSrc);

		await buffer.CopyToAsync(texture);
		await texture.TransitionLayout(ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);

		return texture;
	}

	public static async Task<Texture> CreateAsync(int width, int height, int depth, uint[] data)
	{
		var texture = new Texture(width, height, depth, Format.B8G8R8A8UNorm, ImageType.Generic3D, ImageUsage.TransferDst | ImageUsage.Sampled, ImageAspect.Color);
		await texture.TransitionLayout(ImageLayout.Undefined, ImageLayout.TransferDstOptimal);

		using var buffer = await Buffer.CreateAsync(data, BufferUsage.TransferSrc);

		await buffer.CopyToAsync(texture);
		await texture.TransitionLayout(ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);

		return texture;
	}

	public static async Task<Texture> CreateAsync(int width, int height, Color[] data)
	{
		var texture = new Texture(width, height, 1, Format.R32G32B32A32SFloat, ImageType.Generic2D, ImageUsage.TransferDst | ImageUsage.Sampled, ImageAspect.Color);
		await texture.TransitionLayout(ImageLayout.Undefined, ImageLayout.TransferDstOptimal);

		using var buffer = await Buffer.CreateAsync(data, BufferUsage.TransferSrc);

		await buffer.CopyToAsync(texture);
		await texture.TransitionLayout(ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);

		return texture;
	}

	public static async Task<Texture> CreateAsync(int width, int height, int depth, Color[] data)
	{
		var texture = new Texture(width, height, depth, Format.R32G32B32A32SFloat, ImageType.Generic3D, ImageUsage.TransferDst | ImageUsage.Sampled, ImageAspect.Color);
		await texture.TransitionLayout(ImageLayout.Undefined, ImageLayout.TransferDstOptimal);

		using var buffer = await Buffer.CreateAsync(data, BufferUsage.TransferSrc);

		await buffer.CopyToAsync(texture);
		await texture.TransitionLayout(ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);

		return texture;
	}

	public static async Task<Texture> CreateAsync(string filename)
	{
		if (filename == null)
			throw new ArgumentNullException();

		var extension = Path.GetExtension(filename).ToLower();

		switch (extension)
		{
			case ".png":
				var png = PNG.FromFile(filename);
				return await CreateAsync(png.Width, png.Height, png.Colors);
			default:
				throw new InvalidOperationException($"Failed to parse texture of type '{extension}'.");
		}
	}

	private Texture(int width, int height, int depth, Format format, ImageType type, ImageUsage usage, ImageAspect aspect)
	{
		if (width <= 0 || height <= 0 || depth <= 0)
			throw new ArgumentOutOfRangeException();

		this.Width = width;
		this.Height = height;
		this.Depth = depth;
		this.Format = format;
		this.Type = type;
		this.Usage = usage;
		this.Aspect = aspect;

		using var imageCreateInfo = new ImageCreateInfo(
			next: default,
			flags: default,
			imageType: type,
			format: format,
			extent: new(width: (uint)this.Width, height: (uint)this.Height, depth: (uint)this.Depth),
			mipLevels: 1,
			arrayLayers: 1,
			samples: SampleCount.Bit1,
			tiling: ImageTiling.Optimal,
			usage: usage,
			sharingMode: SharingMode.Concurrent,
			queueFamilyIndices: renderer.QueueFamilyIndices,
			initialLayout: ImageLayout.Undefined
		);

		this.Image = imageCreateInfo.CreateImage(renderer.Device, renderer.Allocator);

		var mem = this.Image.MemoryRequirements;
		var imageMemoryAllocateInfo = new MemoryAllocateInfo(
			next: default,
			allocationSize: mem.Size,
			memoryTypeIndex: renderer.FindMemoryType(mem.MemoryType, MemoryProperty.DeviceLocal)
		);

		this.ImageMemory = imageMemoryAllocateInfo.CreateDeviceMemory(renderer.Device, renderer.Allocator);
		this.ImageMemory.Bind(this.Image);

		var imageViewCreateInfo = new ImageViewCreateInfo(
			next: default,
			flags: default,
			image: this.Image,
			viewType: this.Type switch
			{
				ImageType.Generic1D => ImageViewType.Generic1D,
				ImageType.Generic2D => ImageViewType.Generic2D,
				ImageType.Generic3D => ImageViewType.Generic3D,
				_ => throw new InvalidOperationException($"Cannot map {nameof(Vulkan.ImageType)}.{this.Type} to {nameof(Vulkan.ImageViewType)}.")
			},
			format: format,
			components: new(r: ComponentSwizzle.Identity, g: ComponentSwizzle.Identity, b: ComponentSwizzle.Identity, a: ComponentSwizzle.Identity),
			subresourceRange: new(
				aspect: aspect,
				baseMipLevel: 0,
				levelCount: 1,
				baseArrayLayer: 0,
				layerCount: 1
			)
		);

		this.ImageView = imageViewCreateInfo.CreateImageView(renderer.Device, renderer.Allocator);

		var filter = ((renderer.PhysicalDevice.GetFormatProperties(this.Format).OptimalTilingFeatures & FormatFeatures.SampledImageFilterLinear) != 0) ? Filter.Linear : Filter.Nearest;

		var samplerCreateInfo = new SamplerCreateInfo(
			next: default,
			flags: default,
			magFilter: filter,
			minFilter: filter,
			mipmapMode: SamplerMipmapMode.Linear,
			addressModeU: SamplerAddressMode.Repeat,
			addressModeV: SamplerAddressMode.Repeat,
			addressModeW: SamplerAddressMode.Repeat,
			mipLodBias: 0f,
			anisotropyEnable: false,
			maxAnisotropy: 1f,
			compareEnable: false,
			compareOp: CompareOp.Always,
			minLod: 0f,
			maxLod: 0f,
			borderColor: BorderColor.FloatOpaqueBlack,
			unnormalizedCoordinates: false
		);

		this.Sampler = samplerCreateInfo.CreateSampler(renderer.Device, renderer.Allocator);
	}
}

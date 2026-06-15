using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Vulkan;

using Buffer = Vulkan.Buffer;

namespace Renderer;

public partial class Renderer
{
	public void CreateBuffer(DeviceSize size, BufferUsage usage, out Buffer buffer)
	{
		using var createInfo = new BufferCreateInfo(
			type: StructureType.BufferCreateInfo,
			next: default,
			flags: default,
			size: size,
			usage: usage,
			sharingMode: SharingMode.Exclusive,
			queueFamilyIndices: default
		);

		buffer = createInfo.CreateBuffer(device, allocator);
	}

	public void CreateBufferMemory(Buffer buffer, MemoryProperty properties, out DeviceMemory memory)
	{
		var memoryRequirements = buffer.MemoryRequirements;

		var allocateInfo = new MemoryAllocateInfo(
			type: StructureType.MemoryAllocateInfo,
			next: default,
			allocationSize: memoryRequirements.Size,
			memoryTypeIndex: FindMemoryType(memoryRequirements.MemoryType, properties)
		);

		memory = allocateInfo.CreateDeviceMemory(device, allocator);
		memory.Bind(buffer);
	}

	public void CopyBuffer(Buffer source, Buffer destination, DeviceSize size, CommandBuffer? cmd = null)
	{
		bool createCmd = cmd == null;

		if (createCmd)
			cmd = BeginSingleTimeCommand();

		cmd!.CopyBuffer(source, destination, size);

		if (createCmd)
			EndSingleTimeCommand(cmd);
	}

	public void CopyBufferToImage(Buffer buffer, Image image, int width, int height, ImageAspect aspect = ImageAspect.Color, CommandBuffer? cmd = null)
	{
		bool createCmd = cmd == null;

		if (createCmd)
			cmd = BeginSingleTimeCommand();

		var region = new BufferImageCopy(
			bufferOffset: 0,
			bufferRowLength: 0,
			bufferImageHeight: 0,
			imageSubresource: new(
				aspect: ImageAspect.Color,
				mipLevel: 0,
				baseArrayLayer: 0,
				layerCount: 1
			),
			imageOffset: new(x: 0, y: 0, z: 0),
			imageExtent: new(width: (uint)width, height: (uint)height, depth: 1)
		);

		cmd!.CopyBufferToImage(buffer, image, ImageLayout.TransferDstOptimal, region);

		if (createCmd)
			EndSingleTimeCommand(cmd);
	}

	public void CopyImageToBuffer(Image image, Buffer buffer, int width, int height, ImageAspect aspect = ImageAspect.Color, CommandBuffer? cmd = null)
	{
		bool createCmd = cmd == null;

		if (createCmd)
			cmd = BeginSingleTimeCommand();

		var region = new BufferImageCopy(
			bufferOffset: 0,
			bufferRowLength: 0,
			bufferImageHeight: 0,
			imageSubresource: new(
				aspect: aspect,
				mipLevel: 0,
				baseArrayLayer: 0,
				layerCount: 1
			),
			imageOffset: new(x: 0, y: 0, z: 0),
			imageExtent: new(width: (uint)width, height: (uint)height, depth: 1)
		);

		cmd!.CopyImageToBuffer(image, buffer, ImageLayout.TransferSrcOptimal, region);

		if (createCmd)
			EndSingleTimeCommand(cmd);
	}

	public unsafe void CreateVertexBuffer(Array data, out Buffer buffer, out DeviceMemory memory)
	{
		DeviceSize size = (ulong)Marshal.SizeOf(data.GetValue(0)!.GetType()) * (ulong)data.LongLength;

		CreateBuffer(size, BufferUsage.TransferSrc, out Buffer staggingBuffer);
		CreateBufferMemory(staggingBuffer, MemoryProperty.HostVisible | MemoryProperty.DeviceLocal, out DeviceMemory staggingMemory);

		nint staggingLocation = staggingMemory.Map(size: size, offset: default, flags: default);
		Unsafe.CopyBlockUnaligned(ref Unsafe.AsRef<byte>((void*)staggingLocation), ref MemoryMarshal.GetArrayDataReference(data), checked((uint)size));

		CreateBuffer(size, BufferUsage.TransferDst | BufferUsage.VertexBuffer, out buffer);
		CreateBufferMemory(buffer, MemoryProperty.HostVisible | MemoryProperty.DeviceLocal, out memory);

		CopyBuffer(staggingBuffer, buffer, size);

		staggingMemory.Unmap();
		staggingBuffer.Dispose();
		staggingMemory.Dispose();
	}

	public unsafe void CreateIndexBuffer(Array data, out Buffer buffer, out DeviceMemory memory)
	{
		DeviceSize size = (ulong)Marshal.SizeOf(data.GetValue(0)!.GetType()) * (ulong)data.LongLength;

		CreateBuffer(size, BufferUsage.TransferSrc, out Buffer staggingBuffer);
		CreateBufferMemory(staggingBuffer, MemoryProperty.HostVisible | MemoryProperty.DeviceLocal, out DeviceMemory staggingMemory);

		nint staggingLocation = staggingMemory.Map(size: size, offset: default, flags: default);
		Unsafe.CopyBlockUnaligned(ref Unsafe.AsRef<byte>((void*)staggingLocation), ref MemoryMarshal.GetArrayDataReference(data), checked((uint)size));

		CreateBuffer(size, BufferUsage.TransferDst | BufferUsage.IndexBuffer, out buffer);
		CreateBufferMemory(buffer, MemoryProperty.HostVisible | MemoryProperty.DeviceLocal, out memory);

		CopyBuffer(staggingBuffer, buffer, size);

		staggingMemory.Unmap();
		staggingBuffer.Dispose();
		staggingMemory.Dispose();
	}

	public DeviceSize CreateUniformsBuffer(IReadOnlyDictionary<string, object> data, out Buffer? buffer, out DeviceMemory? memory)
	{
		DeviceSize size = 0;

		foreach ((var key, var value) in data)
			if (value.GetType().IsValueType && !value.GetType().IsGenericType)
				size += (ulong)Marshal.SizeOf(value.GetType());

		if (size == 0)
		{
			(buffer, memory) = (null, null);
			return 0;
		}

		CreateBuffer(size, BufferUsage.UniformBuffer, out buffer);
		CreateBufferMemory(buffer, MemoryProperty.HostVisible | MemoryProperty.HostCoherent, out memory);

		nint mapped = memory.Map(size, offset: default, flags: default);

		foreach ((var key, var value) in data)
		{
			if (!value.GetType().IsValueType && !value.GetType().IsGenericType)
				continue;

			Marshal.StructureToPtr(value, mapped, false);
			mapped += (nint)Marshal.SizeOf(value.GetType());
		}

		memory.Unmap();
		return size;
	}

	public unsafe void CreateTexture(ref byte data, int width, int height, ImageType type, Format format, out Image image, out ImageView imageView, out DeviceMemory memory, out Sampler sampler)
	{
		DeviceSize stride = format switch
		{
			Format.R8G8B8A8UNorm => 4,
			Format.B8G8R8A8UNorm => 4,
			_ => throw new InvalidOperationException($"Failed to map texture format '{format}' to its stride.")
		};

		DeviceSize size = (ulong)width * (ulong)height * stride;

		CreateBuffer(size, BufferUsage.TransferSrc, out Buffer staggingBuffer);
		CreateBufferMemory(staggingBuffer, MemoryProperty.HostVisible | MemoryProperty.DeviceLocal, out DeviceMemory staggingMemory);

		nint staggingLocation = staggingMemory.Map(size: size, offset: default, flags: default);
		Unsafe.CopyBlockUnaligned(ref Unsafe.AsRef<byte>((void*)staggingLocation), ref data, checked((uint)size));

		CreateImage(width, height, type, ImageUsage.TransferDst | ImageUsage.Sampled, format, out image);
		CreateImageMemory(image, out memory);

		TransitionImageLayout(image, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);
		CopyBufferToImage(staggingBuffer, image, width, height);
		TransitionImageLayout(image, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);

		CreateImageView(image, format, ImageAspect.Color, out imageView);
		CreateSampler(out sampler);

		staggingMemory.Unmap();
		staggingBuffer.Dispose();
		staggingMemory.Dispose();
	}

	public void CreateImage(int width, int height, ImageType type, ImageUsage usage, Format format, out Image image)
	{
		using var createInfo = new ImageCreateInfo(
			type: StructureType.ImageCreateInfo,
			next: default,
			flags: default,
			imageType: type,
			format: format,
			extent: new(width: checked((uint)width), height: checked((uint)height), depth: 1),
			mipLevels: 1,
			arrayLayers: 1,
			samples: SampleCount.Bit1,
			tiling: ImageTiling.Optimal,
			usage: usage,
			sharingMode: SharingMode.Exclusive,
			queueFamilyIndices: null,
			initialLayout: ImageLayout.Undefined
		);

		image = createInfo.CreateImage(device, allocator);
	}

	public void CreateImageMemory(Image image, out DeviceMemory memory)
	{
		var mem = image.MemoryRequirements;

		var allocateInfo = new MemoryAllocateInfo(
			type: StructureType.MemoryAllocateInfo,
			next: default,
			allocationSize: mem.Size,
			memoryTypeIndex: FindMemoryType(mem.MemoryType, MemoryProperty.DeviceLocal)
		);

		memory = allocateInfo.CreateDeviceMemory(device, allocator);
		memory.Bind(image);
	}

	public void CreateImageView(Image image, Format format, ImageAspect aspect, out ImageView imageView)
	{
		var createInfo = new ImageViewCreateInfo(
			type: StructureType.ImageViewCreateInfo,
			next: default,
			flags: default,
			image: image,
			viewType: ImageViewType.Generic2D,
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

		imageView = createInfo.CreateImageView(device, allocator);
	}

	public void CreateSampler(out Sampler sampler)
	{
		var createInfo = new SamplerCreateInfo(
			type: StructureType.SamplerCreateInfo,
			next: default,
			flags: default,
			magFilter: Filter.Linear,
			minFilter: Filter.Linear,
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

		sampler = createInfo.CreateSampler(device, allocator);
	}

	public void TransitionImageLayout(
		Image image,

		ImageLayout from,
		Access sourceAccess,
		PipelineStage sourceStage,

		ImageLayout to,
		Access destinationAccess,
		PipelineStage destinationStage,

		CommandBuffer? cmd = null
	)
	{
		bool createCmd = cmd == null;

		if (createCmd)
			cmd = BeginSingleTimeCommand();

		var barrier = new ImageMemoryBarrier(
			type: StructureType.ImageMemoryBarrier,
			next: default,
			srcAccess: sourceAccess,
			dstAccess: destinationAccess,
			oldLayout: from,
			newLayout: to,
			srcQueueFamilyIndex: ~0u,
			dstQueueFamilyIndex: ~0u,
			image: image,
			subresourceRange: new(
				aspect: ImageAspect.Color,
				baseMipLevel: 0,
				levelCount: 1,
				baseArrayLayer: 0,
				layerCount: 1
			)
		);

		cmd!.PipelineBarrier(
			srcStage: sourceStage,
			dstStage: destinationStage,
			dependencyFlags: default,
			memoryBarriers: null,
			bufferMemoryBarriers: null,
			imageMemoryBarriers: [barrier]
		);

		if (createCmd)
			EndSingleTimeCommand(cmd);
	}

	public void TransitionImageLayout(Image image, ImageLayout from, ImageLayout to, CommandBuffer? cmd = null)
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
		else
			throw new InvalidOperationException($"Unsupported layer transition from '{from}' to '{to}'.");

		TransitionImageLayout(image, from, sourceAccess, sourceStage, to, destinationAccess, destinationStage, cmd);
	}

	public CommandBuffer BeginSingleTimeCommand()
	{
		var allocateInfo = new CommandBufferAllocateInfo(
			type: StructureType.CommandBufferAllocateInfo,
			next: default,
			commandPool: commandPool,
			level: CommandBufferLevel.Primary,
			commandBufferCount: 1
		);

		var cmd = allocateInfo.CreateCommandBuffers(device, commandPool).Single();

		using var beginInfo = new CommandBufferBeginInfo(
			type: StructureType.CommandBufferBeginInfo,
			next: default,
			usage: CommandBufferUsage.OneTimeSubmit,
			inheritanceInfo: null
		);

		cmd.Begin(beginInfo);
		return cmd;
	}

	public void EndSingleTimeCommand(CommandBuffer cmd)
	{
		cmd.End();

		using var submitInfo = new SubmitInfo(
			type: StructureType.SubmitInfo,
			next: default,
			waitSemaphores: null,
			waitDstStageMasks: null,
			commandBuffers: [cmd],
			signalSemaphores: null
		);

		graphicsQueue.Submit(null, submitInfo);
		graphicsQueue.WaitIdle();

		cmd.Dispose();
	}
}

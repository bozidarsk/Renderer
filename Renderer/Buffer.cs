using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using Vulkan;

namespace Renderer;

public class Buffer : Asset
{
	private bool mapped = false;

	public ulong Size { get; }
	public BufferUsage Usage { get; }
	public MemoryProperty MemoryProperty { get; }

	internal Vulkan.Buffer VkBuffer { get; }
	internal DeviceMemory Memory { get; }

	protected override void Free()
	{
		if (mapped)
			Memory.Unmap();

		renderer.ToBeDisposed(VkBuffer);
		renderer.ToBeDisposed(Memory);
	}

	public async Task CopyToAsync(Buffer destination, ulong? sourceOffset = null, ulong? destinationOffset = null, ulong? size = null)
	{
		if (destination == null)
			throw new ArgumentNullException();

		var region = new BufferCopy(
			sourceOffset: sourceOffset ?? 0,
			destinationOffset: destinationOffset ?? 0,
			size: size ?? this.Size
		);

		await renderer.TransferQueueContext.SubmitAsync(cmd => cmd.CopyBuffer(this.VkBuffer, destination.VkBuffer, region));
	}

	public async Task CopyToAsync(Texture destination, ulong? bufferOffset = null, Offset3D? imageOffset = null, Extent3D? imageExtent = null)
	{
		if (destination == null)
			throw new ArgumentNullException();

		var region = new BufferImageCopy(
			bufferOffset: bufferOffset ?? 0,
			bufferRowLength: 0,
			bufferImageHeight: 0,
			imageSubresource: new(
				aspect: destination.Aspect,
				mipLevel: 0,
				baseArrayLayer: 0,
				layerCount: 1
			),
			imageOffset: imageOffset ?? new(x: 0, y: 0, z: 0),
			imageExtent: imageExtent ?? new(width: (uint)destination.Width, height: (uint)destination.Height, depth: (uint)destination.Depth)
		);

		await renderer.TransferQueueContext.SubmitAsync(cmd => cmd.CopyBufferToImage(this.VkBuffer, destination.Image, ImageLayout.TransferDstOptimal, region));
	}

	public unsafe Span<T> Map<T>() where T : struct
	{
		if (mapped)
			throw new InvalidOperationException("This buffer is already mapped.");

		T* location = (T*)Memory.Map(size: Size, offset: default, flags: default);
		int length = checked((int)(Size / (ulong)sizeof(T)));

		mapped = true;

		return new Span<T>(location, length);
	}

	public void Unmap()
	{
		if (!mapped)
			throw new InvalidOperationException("This buffer was not mapped.");

		Memory.Unmap();

		mapped = false;
	}

	public static Buffer Create(ulong size, BufferUsage usage, MemoryProperty memoryProperty) => new(size, usage, memoryProperty);

	public static async Task<Buffer> CreateAsync<T>(T[] data, BufferUsage usage) where T : struct
	{
		if (data == null)
			throw new ArgumentNullException();

		DeviceSize size = (ulong)Marshal.SizeOf(typeof(T)) * (ulong)data.LongLength;

		using var hostBuffer = new Buffer(size, BufferUsage.TransferSrc, MemoryProperty.HostVisible | MemoryProperty.HostCoherent);

		var map = hostBuffer.Map<T>();
		data.CopyTo(map);
		hostBuffer.Unmap();

		var deviceBuffer = new Buffer(size, BufferUsage.TransferDst | usage, MemoryProperty.DeviceLocal);

		if (hostBuffer.renderer != deviceBuffer.renderer)
			throw new InvalidOperationException();

		await hostBuffer.CopyToAsync(deviceBuffer);

		return deviceBuffer;
	}

	private Buffer(ulong size, BufferUsage usage, MemoryProperty memoryProperty)
	{
		if (size == 0)
			throw new ArgumentOutOfRangeException(nameof(size), "Cannot create a zero-sized buffer.");

		this.Size = size;
		this.Usage = usage;
		this.MemoryProperty = memoryProperty;

		using var createInfo = new BufferCreateInfo(
			next: default,
			flags: default,
			size: size,
			usage: usage,
			sharingMode: SharingMode.Concurrent,
			queueFamilyIndices: renderer.QueueFamilyIndices
		);

		this.VkBuffer = createInfo.CreateBuffer(renderer.Device, renderer.Allocator);

		var memoryRequirements = this.VkBuffer.MemoryRequirements;

		var allocateInfo = new MemoryAllocateInfo(
			next: default,
			allocationSize: memoryRequirements.Size,
			memoryTypeIndex: renderer.FindMemoryType(memoryRequirements.MemoryType, memoryProperty)
		);

		this.Memory = allocateInfo.CreateDeviceMemory(renderer.Device, renderer.Allocator);
		this.Memory.Bind(this.VkBuffer);
	}
}

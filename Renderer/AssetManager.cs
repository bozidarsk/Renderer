using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Linq;
using System.IO;

using Vulkan;

using Buffer = Vulkan.Buffer;

namespace Renderer;

internal record ShaderProgramData(ShaderModule[] Modules, PipelineShaderStageCreateInfo[] Stages) : IDisposable
{
	public void Dispose()
	{
		foreach (var x in Stages)
			x.Dispose();

		foreach (var x in Modules)
			x.Dispose();
	}
}

internal record MeshData(Buffer VertexBuffer, DeviceMemory VertexBufferMemory, Buffer IndexBuffer, DeviceMemory IndexBufferMemory, IndexType IndexType) : IDisposable
{
	public void Dispose()
	{
		VertexBuffer.Dispose();
		VertexBufferMemory.Dispose();
		IndexBuffer.Dispose();
		IndexBufferMemory.Dispose();
	}
}

internal record TextureData(Image Image, ImageView ImageView, DeviceMemory ImageMemory, Sampler Sampler) : IDisposable
{
	public void Dispose()
	{
		Image.Dispose();
		ImageView.Dispose();
		ImageMemory.Dispose();
		Sampler.Dispose();
	}
}

internal record RenderTargetData(RenderingInfo RenderingInfo, ImageMemoryBarrier2[] BeginDependencies, ImageMemoryBarrier2[] EndDependencies) : IDisposable
{
	public void Dispose()
	{
		RenderingInfo.Dispose();
	}
}

internal class AssetManager : IDisposable
{
	private readonly Renderer renderer;

	private readonly Dictionary<ShaderProgram, ShaderProgramData> shaderPrograms = new();
	private readonly Dictionary<Mesh, MeshData> meshes = new();
	private readonly Dictionary<Texture, TextureData> textures = new();
	private readonly Dictionary<RenderTarget, RenderTargetData> renderTargets = new();

	public ShaderProgramData GetShaderProgramData(ShaderProgram shaderProgram)
	{
		if (shaderProgram == null)
			throw new ArgumentNullException();

		if (shaderPrograms.TryGetValue(shaderProgram, out ShaderProgramData? shaderProgramData))
			return shaderProgramData!;

		var shaders = shaderProgram.Shaders;
		shaderProgramData = new(
			new ShaderModule[shaders.Length],
			new PipelineShaderStageCreateInfo[shaders.Length]
		);

		for (int i = 0; i < shaders.Length; i++)
		{
			using ShaderModuleCreateInfo shaderModuleCreateInfo = new(
				next: default,
				flags: default,
				code: shaders[i].Code
			);

			var module = shaderModuleCreateInfo.CreateShaderModule(renderer.Device, renderer.Allocator);
			var stage = new PipelineShaderStageCreateInfo(
				next: default,
				flags: default,
				stage: shaders[i].Stage,
				module: module,
				name: shaders[i].EntryPoint,
				specializationInfo: null
			);

			shaderProgramData.Modules[i] = module;
			shaderProgramData.Stages[i] = stage;
		}

		shaderPrograms[shaderProgram] = shaderProgramData;
		return shaderProgramData;
	}

	public MeshData GetMeshData(Mesh mesh)
	{
		if (meshes.TryGetValue(mesh, out MeshData? meshData))
			return meshData!;

		renderer.CreateStagingBuffer(mesh.Vertices, BufferUsage.VertexBuffer, out Buffer vertexBuffer, out DeviceMemory vertexBufferMemory);
		renderer.CreateStagingBuffer(mesh.Indices, BufferUsage.IndexBuffer, out Buffer indexBuffer, out DeviceMemory indexBufferMemory);

		meshData = new(
			vertexBuffer,
			vertexBufferMemory,
			indexBuffer,
			indexBufferMemory,
			mesh.IndexType switch
			{
				Type t when t == typeof(byte) => IndexType.UInt8,
				Type t when t == typeof(ushort) => IndexType.UInt16,
				Type t when t == typeof(uint) => IndexType.UInt32,
				_ => throw new ArgumentOutOfRangeException(nameof(IndexType), $"Cannot map mesh index type '{mesh.IndexType.FullName}' to a vulkan index type.")
			}
		);

		meshes[mesh] = meshData;
		return meshData;
	}

	public TextureData GetTextureData(Texture texture)
	{
		if (textures.TryGetValue(texture, out TextureData? textureData))
			return textureData!;

		Image image;
		ImageView imageView;
		DeviceMemory imageMemory;
		Sampler sampler;

		if (texture.Data != null)
		{
			DeviceSize stride = texture.Format switch
			{
				Format.R8G8B8A8UNorm => 4,
				Format.B8G8R8A8UNorm => 4,
				Format.R8G8B8A8SRGB => 4,
				Format.R8G8B8A8UInt => 4,
				Format.R32G32B32A32SFloat => 16,
				Format.D32SFloat => 4,
				Format.D32SFloatS8UInt => 8,
				Format.D24UNormS8UInt => 4,
				_ => throw new InvalidOperationException($"Failed to map texture format '{texture.Format}' to its stride.")
			};

			DeviceSize size = (ulong)texture.Width * (ulong)texture.Height * stride;

			renderer.CreateBuffer(size, BufferUsage.TransferSrc, out Buffer stagingBuffer);
			renderer.CreateBufferMemory(stagingBuffer, MemoryProperty.HostVisible | MemoryProperty.DeviceLocal, out DeviceMemory stagingMemory);

			unsafe
			{
				nint stagingLocation = stagingMemory.Map(size: size, offset: default, flags: default);
				Unsafe.CopyBlockUnaligned(ref Unsafe.AsRef<byte>((void*)stagingLocation), ref MemoryMarshal.GetArrayDataReference(texture.Data), checked((uint)size));
			}

			renderer.CreateImage(texture.Width, texture.Height, texture.Type, texture.Usage, texture.Format, out image);
			renderer.CreateImageMemory(image, out imageMemory);

			renderer.TransitionImageLayout(image, ImageLayout.Undefined, ImageLayout.TransferDstOptimal, texture.Aspect);
			renderer.CopyBufferToImage(stagingBuffer, image, texture.Width, texture.Height, texture.Aspect);
			renderer.TransitionImageLayout(image, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal, texture.Aspect);

			renderer.CreateImageView(image, texture.Format, texture.Aspect, ImageViewType.Generic2D, out imageView);
			renderer.CreateSampler(out sampler, ((renderer.PhysicalDevice.GetFormatProperties(texture.Format).OptimalTilingFeatures & FormatFeatures.SampledImageFilterLinear) != 0) ? Filter.Linear : Filter.Nearest);

			stagingMemory.Unmap();
			stagingBuffer.Dispose();
			stagingMemory.Dispose();
		}
		else
		{
			renderer.CreateImage(texture.Width, texture.Height, texture.Type, texture.Usage, texture.Format, out image);
			renderer.CreateImageMemory(image, out imageMemory);
			renderer.CreateImageView(image, texture.Format, texture.Aspect, ImageViewType.Generic2D, out imageView);
			renderer.CreateSampler(out sampler, ((renderer.PhysicalDevice.GetFormatProperties(texture.Format).OptimalTilingFeatures & FormatFeatures.SampledImageFilterLinear) != 0) ? Filter.Linear : Filter.Nearest);

			renderer.TransitionImageLayout(image, ImageLayout.Undefined, ImageLayout.ShaderReadOnlyOptimal, texture.Aspect);
		}

		textureData = new(image, imageView, imageMemory, sampler);

		textures[texture] = textureData;
		return textureData;
	}

	public RenderTargetData GetRenderTargetData(RenderTarget renderTarget)
	{
		if (renderTargets.TryGetValue(renderTarget, out RenderTargetData? renderTargetData))
			return renderTargetData!;

		var renderingInfo = new RenderingInfo(
			next: default,
			flags: default,
			renderArea: new(offset: new(0, 0), extent: new((uint)renderTarget.Width, (uint)renderTarget.Height)),
			layerCount: 1,
			viewMask: 0,
			colorAttachments: renderTarget.ColorAttachments.Select(x => new RenderingAttachmentInfo(
					next: default,
					imageView: renderer.AssetManager.GetTextureData(x.Texture).ImageView,
					imageLayout: ImageLayout.ColorAttachmentOptimal,
					resolveMode: ResolveMode.None,
					resolveImageView: null,
					resolveImageLayout: ImageLayout.Undefined,
					loadOp: x.LoadOp,
					storeOp: x.StoreOp,
					clearValue: x.ClearValue
				)
			).ToArray(),
			depthAttachment: (renderTarget.DepthAttachment != null)
				? new RenderingAttachmentInfo(
						next: default,
						imageView: renderer.AssetManager.GetTextureData(renderTarget.DepthAttachment.Texture).ImageView,
						imageLayout: ImageLayout.DepthAttachmentOptimal,
						resolveMode: ResolveMode.None,
						resolveImageView: null,
						resolveImageLayout: ImageLayout.Undefined,
						loadOp: renderTarget.DepthAttachment.LoadOp,
						storeOp: renderTarget.DepthAttachment.StoreOp,
						clearValue: renderTarget.DepthAttachment.ClearValue
					)
				: null,
			stencilAttachment: (renderTarget.StencilAttachment != null)
				? new RenderingAttachmentInfo(
						next: default,
						imageView: renderer.AssetManager.GetTextureData(renderTarget.StencilAttachment.Texture).ImageView,
						imageLayout: ImageLayout.StencilAttachmentOptimal,
						resolveMode: ResolveMode.None,
						resolveImageView: null,
						resolveImageLayout: ImageLayout.Undefined,
						loadOp: renderTarget.StencilAttachment.LoadOp,
						storeOp: renderTarget.StencilAttachment.StoreOp,
						clearValue: renderTarget.StencilAttachment.ClearValue
					)
				: null
		);

		var beginDependencies = renderTarget.BeginDependencies.Select(x => new ImageMemoryBarrier2(
				next: default,
				srcStage: x.SrcStage,
				srcAccess: x.SrcAccess,
				dstStage: x.DstStage,
				dstAccess: x.DstAccess,
				oldLayout: x.OldLayout,
				newLayout: x.NewLayout,
				srcQueueFamilyIndex: ~0u,
				dstQueueFamilyIndex: ~0u,
				image: renderer.AssetManager.GetTextureData(renderTarget.ColorAttachments[x.Attachment].Texture).Image,
				subresourceRange: new ImageSubresourceRange(renderTarget.ColorAttachments[x.Attachment].Texture.Aspect, 0, 1, 0, 1)
			)
		).ToArray();

		var endDependencies = renderTarget.EndDependencies.Select(x => new ImageMemoryBarrier2(
				next: default,
				srcStage: x.SrcStage,
				srcAccess: x.SrcAccess,
				dstStage: x.DstStage,
				dstAccess: x.DstAccess,
				oldLayout: x.OldLayout,
				newLayout: x.NewLayout,
				srcQueueFamilyIndex: ~0u,
				dstQueueFamilyIndex: ~0u,
				image: renderer.AssetManager.GetTextureData(renderTarget.ColorAttachments[x.Attachment].Texture).Image,
				subresourceRange: new ImageSubresourceRange(renderTarget.ColorAttachments[x.Attachment].Texture.Aspect, 0, 1, 0, 1)
			)
		).ToArray();

		renderTargetData = new(renderingInfo, beginDependencies, endDependencies);

		renderTargets[renderTarget] = renderTargetData;
		return renderTargetData;
	}

	public void Dispose()
	{
		foreach (var x in shaderPrograms.Values)
			x.Dispose();

		foreach (var x in meshes.Values)
			x.Dispose();

		foreach (var x in textures.Values)
			x.Dispose();

		foreach (var x in renderTargets.Values)
			x.Dispose();
	}

	public AssetManager(Renderer renderer)
	{
		this.renderer = renderer ?? throw new ArgumentNullException();
	}
}

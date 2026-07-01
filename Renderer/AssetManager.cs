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
			renderer.CreateStagingBuffer(texture.Data, BufferUsage.TransferSrc, out Buffer buffer, out DeviceMemory memory);

			renderer.CreateImage(texture.Width, texture.Height, texture.Type, texture.Usage, texture.Format, out image);
			renderer.CreateImageMemory(image, out imageMemory);

			renderer.TransitionImageLayout(image, ImageLayout.Undefined, ImageLayout.TransferDstOptimal, texture.Aspect);
			renderer.CopyBufferToImage(buffer, image, texture.Width, texture.Height, texture.Aspect);
			renderer.TransitionImageLayout(image, ImageLayout.TransferDstOptimal, texture.InitialLayout, texture.Aspect);

			renderer.CreateImageView(image, texture.Format, texture.Aspect, ImageViewType.Generic2D, out imageView);
			renderer.CreateSampler(out sampler, ((renderer.PhysicalDevice.GetFormatProperties(texture.Format).OptimalTilingFeatures & FormatFeatures.SampledImageFilterLinear) != 0) ? Filter.Linear : Filter.Nearest);

			memory.Dispose();
			buffer.Dispose();
		}
		else
		{
			renderer.CreateImage(texture.Width, texture.Height, texture.Type, texture.Usage, texture.Format, out image);
			renderer.CreateImageMemory(image, out imageMemory);
			renderer.CreateImageView(image, texture.Format, texture.Aspect, texture.Type switch
			{
				ImageType.Generic1D => ImageViewType.Generic1D,
				ImageType.Generic2D => ImageViewType.Generic2D,
				ImageType.Generic3D => ImageViewType.Generic3D,
				_ => throw new InvalidOperationException($"Cannot map ImageType.{texture.Type} to ImageViewType.")
			}, out imageView);
			renderer.CreateSampler(out sampler, ((renderer.PhysicalDevice.GetFormatProperties(texture.Format).OptimalTilingFeatures & FormatFeatures.SampledImageFilterLinear) != 0) ? Filter.Linear : Filter.Nearest);

			renderer.TransitionImageLayout(image, ImageLayout.Undefined, texture.InitialLayout, texture.Aspect);
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

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Linq;

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

internal record MeshData(Buffer VertexBuffer, DeviceMemory VertexBufferMemory, Buffer IndexBuffer, DeviceMemory IndexBufferMemory) : IDisposable
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

internal record RenderTextureData(RenderPass RenderPass, Framebuffer Framebuffer, Texture ColorTexture, Texture DepthTexture) : IDisposable
{
	public void Dispose()
	{
		RenderPass.Dispose();
		Framebuffer.Dispose();
	}
}

internal class AssetManager : IDisposable
{
	private readonly Renderer renderer;

	private readonly Dictionary<ShaderProgram, ShaderProgramData> shaderPrograms = new();
	private readonly Dictionary<Mesh, MeshData> meshes = new();
	private readonly Dictionary<Texture, TextureData> textures = new();
	private readonly Dictionary<RenderTexture, RenderTextureData> renderTextures = new();

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

		renderer.CreateVertexBuffer(mesh.Vertices, out Buffer vertexBuffer, out DeviceMemory vertexBufferMemory);
		renderer.CreateIndexBuffer(mesh.Indices, out Buffer indexBuffer, out DeviceMemory indexBufferMemory);

		meshData = new(vertexBuffer, vertexBufferMemory, indexBuffer, indexBufferMemory);

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

			renderer.CreateBuffer(size, BufferUsage.TransferSrc, out Buffer staggingBuffer);
			renderer.CreateBufferMemory(staggingBuffer, MemoryProperty.HostVisible | MemoryProperty.DeviceLocal, out DeviceMemory staggingMemory);

			unsafe
			{
				nint staggingLocation = staggingMemory.Map(size: size, offset: default, flags: default);
				Unsafe.CopyBlockUnaligned(ref Unsafe.AsRef<byte>((void*)staggingLocation), ref MemoryMarshal.GetArrayDataReference(texture.Data), checked((uint)size));
			}

			renderer.CreateImage(texture.Width, texture.Height, texture.Type, texture.Usage, texture.Format, out image);
			renderer.CreateImageMemory(image, out imageMemory);

			renderer.TransitionImageLayout(image, ImageLayout.Undefined, ImageLayout.TransferDstOptimal, texture.Aspect);
			renderer.CopyBufferToImage(staggingBuffer, image, texture.Width, texture.Height, texture.Aspect);
			renderer.TransitionImageLayout(image, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal, texture.Aspect);

			renderer.CreateImageView(image, texture.Format, texture.Aspect, out imageView);
			renderer.CreateSampler(out sampler);

			staggingMemory.Unmap();
			staggingBuffer.Dispose();
			staggingMemory.Dispose();
		}
		else
		{
			renderer.CreateImage(texture.Width, texture.Height, texture.Type, texture.Usage, texture.Format, out image);
			renderer.CreateImageMemory(image, out imageMemory);
			renderer.CreateImageView(image, texture.Format, texture.Aspect, out imageView);
			renderer.CreateSampler(out sampler);

			renderer.TransitionImageLayout(image, ImageLayout.Undefined, ImageLayout.ShaderReadOnlyOptimal, texture.Aspect);
		}

		textureData = new(image, imageView, imageMemory, sampler);

		textures[texture] = textureData;
		return textureData;
	}

	public RenderTextureData GetRenderTextureData(RenderTexture renderTexture)
	{
		if (renderTextures.TryGetValue(renderTexture, out RenderTextureData? renderTextureData))
			return renderTextureData!;

		var depthFormat = renderer.FindSupportedFormat(
			[Format.D32SFloat, Format.D32SFloatS8UInt, Format.D24UNormS8UInt],
			ImageTiling.Optimal,
			FormatFeatures.DepthStencilAttachment
		);

		var colorTexture = (Texture)renderTexture;
		var depthTexture = new Texture(renderTexture.Width, renderTexture.Height, usage: ImageUsage.DepthStencilAttachment | ImageUsage.Sampled, aspect: ImageAspect.Depth, format: depthFormat, type: ImageType.Generic2D);

		var colorTextureData = GetTextureData(colorTexture);
		var depthTextureData = GetTextureData(depthTexture);

		var colorAttachment = new AttachmentDescription(
			flags: default,
			format: renderTexture.Format,
			samples: SampleCount.Bit1,
			loadOp: AttachmentLoadOp.Clear,
			storeOp: AttachmentStoreOp.Store,
			stencilLoadOp: AttachmentLoadOp.DontCare,
			stencilStoreOp: AttachmentStoreOp.DontCare,
			initialLayout: ImageLayout.Undefined,
			finalLayout: ImageLayout.PresentSrc
		);

		var colorAttachmentRef = new AttachmentReference(
			attachment: 0,
			layout: ImageLayout.ColorAttachmentOptimal
		);

		var depthAttachment = new AttachmentDescription(
			flags: default,
			format: depthFormat,
			samples: SampleCount.Bit1,
			loadOp: AttachmentLoadOp.Clear,
			storeOp: AttachmentStoreOp.DontCare,
			stencilLoadOp: AttachmentLoadOp.DontCare,
			stencilStoreOp: AttachmentStoreOp.DontCare,
			initialLayout: ImageLayout.Undefined,
			finalLayout: ImageLayout.DepthStencilAttachmentOptimal
		);

		var depthAttachmentRef = new AttachmentReference(
			attachment: 1,
			layout: ImageLayout.DepthStencilAttachmentOptimal
		);

		using var subpass = new SubpassDescription(
			flags: default,
			pipelineBindPoint: PipelineBindPoint.Graphics,
			inputAttachments: null,
			colorAttachments: [colorAttachmentRef],
			resolveAttachments: null,
			depthStencilAttachment: depthAttachmentRef,
			preserveAttachments: null
		);

		var dependency = new SubpassDependency(
			srcSubpass: ~0u,
			dstSubpass: 0,
			srcStageMask: PipelineStage.ColorAttachmentOutput | PipelineStage.EarlyFragmentTests,
			dstStageMask: PipelineStage.ColorAttachmentOutput | PipelineStage.EarlyFragmentTests,
			srcAccessMask: 0,
			dstAccessMask: Access.ColorAttachmentWrite | Access.DepthStencilAttachmentWrite,
			dependencyFlags: default
		);

		using var renderPassCreateInfo = new RenderPassCreateInfo(
			next: default,
			flags: default,
			attachments: [colorAttachment, depthAttachment],
			subpasses: [subpass],
			dependencies: [dependency]
		);

		RenderPass renderPass = renderPassCreateInfo.CreateRenderPass(renderer.Device, renderer.Allocator);

		using var framebufferCreateInfo = new FramebufferCreateInfo(
			next: default,
			flags: default,
			renderPass: renderPass,
			attachments: [colorTextureData.ImageView, depthTextureData.ImageView],
			width: (uint)renderTexture.Width,
			height: (uint)renderTexture.Height,
			layers: 1
		);

		Framebuffer framebuffer = framebufferCreateInfo.CreateFramebuffer(renderer.Device, renderer.Allocator);

		renderTextureData = new(renderPass, framebuffer, colorTexture, depthTexture);

		renderTextures[renderTexture] = renderTextureData;
		return renderTextureData;
	}

	public void Dispose()
	{
		foreach (var x in shaderPrograms.Values)
			x.Dispose();

		foreach (var x in meshes.Values)
			x.Dispose();

		foreach (var x in textures.Values)
			x.Dispose();

		foreach (var x in renderTextures.Values)
			x.Dispose();
	}

	public AssetManager(Renderer renderer)
	{
		this.renderer = renderer ?? throw new ArgumentNullException();
	}
}

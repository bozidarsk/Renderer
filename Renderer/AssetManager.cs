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

internal record RenderTextureData(RenderPass RenderPass, Framebuffer Framebuffer) : IDisposable
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

			renderer.CreateImageView(image, texture.Format, texture.Aspect, ImageViewType.Generic2D, out imageView);
			renderer.CreateSampler(out sampler);

			staggingMemory.Unmap();
			staggingBuffer.Dispose();
			staggingMemory.Dispose();
		}
		else
		{
			renderer.CreateImage(texture.Width, texture.Height, texture.Type, texture.Usage, texture.Format, out image);
			renderer.CreateImageMemory(image, out imageMemory);
			renderer.CreateImageView(image, texture.Format, texture.Aspect, ImageViewType.Generic2D, out imageView);
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

		var json = (JsonElement)JsonSerializer.Deserialize<dynamic>(File.ReadAllText(renderTexture.RenderPass.File))!;

		var attachments = json.EnumerateObject().First(p => p.Name == "attachments").Value.EnumerateArray().Select(x => new AttachmentDescription(
				flags: default,
				format: Enum.Parse<Format>(x.EnumerateObject().First(p => p.Name == "format").Value.GetString()!),
				samples: (SampleCount)x.EnumerateObject().First(p => p.Name == "samples").Value.GetInt32(),
				loadOp: Enum.Parse<AttachmentLoadOp>(x.EnumerateObject().First(p => p.Name == "loadOp").Value.GetString()!),
				storeOp: Enum.Parse<AttachmentStoreOp>(x.EnumerateObject().First(p => p.Name == "storeOp").Value.GetString()!),
				stencilLoadOp: Enum.Parse<AttachmentLoadOp>(x.EnumerateObject().First(p => p.Name == "stencilLoadOp").Value.GetString()!),
				stencilStoreOp: Enum.Parse<AttachmentStoreOp>(x.EnumerateObject().First(p => p.Name == "stencilStoreOp").Value.GetString()!),
				initialLayout: Enum.Parse<ImageLayout>(x.EnumerateObject().First(p => p.Name == "initialLayout").Value.GetString()!),
				finalLayout: Enum.Parse<ImageLayout>(x.EnumerateObject().First(p => p.Name == "finalLayout").Value.GetString()!)
			)
		).ToArray();

		var subpasses = json.EnumerateObject().First(p => p.Name == "subpasses").Value.EnumerateArray().Select(x => new SubpassDescription(
				flags: default,
				pipelineBindPoint: Enum.Parse<PipelineBindPoint>(x.EnumerateObject().First(p => p.Name == "pipelineBindPoint").Value.GetString()!),
				inputAttachments: x.EnumerateObject().First(p => p.Name == "inputAttachments").Value.EnumerateArray().Select(y => new AttachmentReference(
						attachment: (uint)y.EnumerateObject().First(p => p.Name == "index").Value.GetInt32(),
						layout: Enum.Parse<ImageLayout>(y.EnumerateObject().First(p => p.Name == "layout").Value.GetString()!)
					)
				).ToArray(),
				colorAttachments: x.EnumerateObject().First(p => p.Name == "colorAttachments").Value.EnumerateArray().Select(y => new AttachmentReference(
						attachment: (uint)y.EnumerateObject().First(p => p.Name == "index").Value.GetInt32(),
						layout: Enum.Parse<ImageLayout>(y.EnumerateObject().First(p => p.Name == "layout").Value.GetString()!)
					)
				).ToArray(),
				resolveAttachments: x.EnumerateObject().First(p => p.Name == "resolveAttachments").Value.EnumerateArray().Select(y => new AttachmentReference(
						attachment: (uint)y.EnumerateObject().First(p => p.Name == "index").Value.GetInt32(),
						layout: Enum.Parse<ImageLayout>(y.EnumerateObject().First(p => p.Name == "layout").Value.GetString()!)
					)
				).ToArray(),
				depthStencilAttachment: (x.EnumerateObject().First(p => p.Name == "depthStencilAttachment").Value.ValueKind != JsonValueKind.Null)
					? new AttachmentReference(
						attachment: (uint)x.EnumerateObject().First(p => p.Name == "depthStencilAttachment").Value.EnumerateObject().First(p => p.Name == "index").Value.GetInt32(),
						layout: Enum.Parse<ImageLayout>(x.EnumerateObject().First(p => p.Name == "depthStencilAttachment").Value.EnumerateObject().First(p => p.Name == "layout").Value.GetString()!)
					)
					: null
				,
				preserveAttachments: x.EnumerateObject().First(p => p.Name == "preserveAttachments").Value.EnumerateArray().Select(x => (uint)x.GetInt32()).ToArray()
			)
		).ToArray();

		var dependencies = json.EnumerateObject().First(p => p.Name == "dependencies").Value.EnumerateArray().Select(x => new SubpassDependency(
				srcSubpass: (uint)x.EnumerateObject().First(p => p.Name == "sourceSubpass").Value.GetInt32(),
				dstSubpass: (uint)x.EnumerateObject().First(p => p.Name == "destinationSubpass").Value.GetInt32(),
				srcStageMask: x.EnumerateObject().First(p => p.Name == "sourceStages").Value.EnumerateArray().Select(x => Enum.Parse<PipelineStage>(x.GetString()!)).Prepend(default).Aggregate((total, next) => total | next),
				dstStageMask: x.EnumerateObject().First(p => p.Name == "destinationStages").Value.EnumerateArray().Select(x => Enum.Parse<PipelineStage>(x.GetString()!)).Prepend(default).Aggregate((total, next) => total | next),
				srcAccessMask: x.EnumerateObject().First(p => p.Name == "sourceAccess").Value.EnumerateArray().Select(x => Enum.Parse<Access>(x.GetString()!)).Prepend(default).Aggregate((total, next) => total | next),
				dstAccessMask: x.EnumerateObject().First(p => p.Name == "destinationAccess").Value.EnumerateArray().Select(x => Enum.Parse<Access>(x.GetString()!)).Prepend(default).Aggregate((total, next) => total | next),
				dependencyFlags: x.EnumerateObject().First(p => p.Name == "dependencyFlags").Value.EnumerateArray().Select(x => Enum.Parse<DependencyFlags>(x.GetString()!)).Prepend(default).Aggregate((total, next) => total | next)
			)
		).ToArray();

		using var renderPassCreateInfo = new RenderPassCreateInfo(
			next: default,
			flags: default,
			attachments: attachments,
			subpasses: subpasses,
			dependencies: dependencies
		);

		RenderPass renderPass = renderPassCreateInfo.CreateRenderPass(renderer.Device, renderer.Allocator);

		foreach (var x in subpasses)
			x.Dispose();

		using var framebufferCreateInfo = new FramebufferCreateInfo(
			next: default,
			flags: default,
			renderPass: renderPass,
			attachments: renderTexture.Images.Select(x => GetTextureData(x).ImageView).ToArray(),
			width: (uint)renderTexture.Width,
			height: (uint)renderTexture.Height,
			layers: 1
		);

		Framebuffer framebuffer = framebufferCreateInfo.CreateFramebuffer(renderer.Device, renderer.Allocator);

		renderTextureData = new(renderPass, framebuffer);

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

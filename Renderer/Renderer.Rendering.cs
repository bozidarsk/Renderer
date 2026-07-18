using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Vulkan;
using Vulkan.ShaderCompiler;

using Buffer = Vulkan.Buffer;
using Renderer.UI;

namespace Renderer;

internal sealed partial class Renderer
{
	private readonly Dictionary<Type, (VertexInputBindingDescription2[] Bindings, VertexInputAttributeDescription2[] Attributes)> vertexInputDescriptions = new();
	private readonly Dictionary<(ShaderProgram, RenderTarget?), Pipeline> graphicsPipelines = new();

	public (VertexInputBindingDescription2[] Bindings, VertexInputAttributeDescription2[] Attributes) CreateVertexInputDescriptions(Type vertexType)
	{
		if (vertexType == null)
			throw new ArgumentNullException();

		VertexInputBindingDescription[] bindingDescriptions = [
			new(
				binding: 0,
				stride: (uint)Marshal.SizeOf(vertexType),
				inputRate: VertexInputRate.Vertex
			)
		];

		VertexInputAttributeDescription[] attributeDescriptions =
			vertexType
			.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.Index()
			.Select(x =>
				new VertexInputAttributeDescription(
					location: (uint)x.Index,
					binding: 0,
					format: x.Item.FieldType switch
					{
						Type t when t == typeof(float) => Format.R32SFloat,
						Type t when t == typeof(double) => Format.R64SFloat,
						Type t when t == typeof(Color) => Format.R32G32B32A32SFloat,
						Type t when t == typeof(Vector2) => Format.R32G32SFloat,
						Type t when t == typeof(Vector3) => Format.R32G32B32SFloat,
						Type t when t == typeof(Vector4) => Format.R32G32B32A32SFloat,
						Type t when t == typeof(Vector2Int) => Format.R32G32SInt,
						Type t when t == typeof(Vector3Int) => Format.R32G32B32SInt,
						Type t when t == typeof(Vector4Int) => Format.R32G32B32A32SInt,
						_ => throw new ArgumentOutOfRangeException(nameof(Type), $"Cannot map field type '{x.Item.FieldType.FullName!}' to a format.")
					},
					offset: (uint)Marshal.OffsetOf(x.Item.DeclaringType!, x.Item.Name)
				)
			)
			.ToArray()
		;

		VertexInputBindingDescription2[] bindingDescriptions2 = bindingDescriptions.Select(x =>
			new VertexInputBindingDescription2(
				next: default,
				description: x,
				divisor: 1
			)
		).ToArray();

		VertexInputAttributeDescription2[] attributeDescriptions2 = attributeDescriptions.Select(x =>
			new VertexInputAttributeDescription2(
				next: default,
				description: x
			)
		).ToArray();

		return (bindingDescriptions2, attributeDescriptions2);
	}

	public unsafe Pipeline CreateGraphicsPipeline(ShaderProgram shaderProgram, RenderTarget? target = null)
	{
		var inputAssembly = new PipelineInputAssemblyStateCreateInfo(
			next: default,
			flags: default,
			topology: PrimitiveTopology.TriangleList,
			primitiveRestartEnable: false
		);

		using var viewport = new PipelineViewportStateCreateInfo(
			next: default,
			flags: default,
			viewports: [default],
			scissors: [default]
		);

		var rasterization = new PipelineRasterizationStateCreateInfo(
			next: default,
			flags: default,
			depthClampEnable: false,
			rasterizerDiscardEnable: false,
			polygonMode: PolygonMode.Fill,
			cullMode: shaderProgram.CullMode ?? CullMode.Back,
			frontFace: shaderProgram.FrontFace ?? FrontFace.CounterClockwise,
			depthBiasEnable: false,
			depthBiasConstantFactor: 0f,
			depthBiasClamp: 0f,
			depthBiasSlopeFactor: 0f,
			lineWidth: 1f
		);

		using var multisample = new PipelineMultisampleStateCreateInfo(
			next: default,
			flags: default,
			rasterizationSamples: SampleCount.Bit1,
			sampleShadingEnable: false,
			minSampleShading: 1f,
			sampleMask: null,
			alphaToCoverageEnable: false,
			alphaToOneEnable: false
		);

		var depthStencil = new PipelineDepthStencilStateCreateInfo(

			next: default,
			flags: default,
			depthTestEnable: true,
			depthWriteEnable: true,
			depthCompareOp: CompareOp.Greater,
			depthBoundsTestEnable: false,
			stencilTestEnable: false,
			front: default,
			back: default,
			minDepthBounds: 0f,
			maxDepthBounds: 1f
		);

		var defaultBlending = new PipelineColorBlendAttachmentState(
			blendEnable: !(shaderProgram.DisableBlending ?? false),
			srcColorBlendFactor: shaderProgram.SourceBlendFactor ?? BlendFactor.One,
			dstColorBlendFactor: shaderProgram.DestinationBlendFactor ?? BlendFactor.Zero,
			colorBlendOp: shaderProgram.BlendOp ?? BlendOp.Add,
			srcAlphaBlendFactor: shaderProgram.SourceBlendFactor ?? BlendFactor.One,
			dstAlphaBlendFactor: shaderProgram.DestinationBlendFactor ?? BlendFactor.Zero,
			alphaBlendOp: shaderProgram.BlendOp ?? BlendOp.Add,
			colorWriteMask: ColorComponent.R | ColorComponent.G | ColorComponent.B | ColorComponent.A
		);

		using var colorBlend = new PipelineColorBlendStateCreateInfo(
			next: default,
			flags: default,
			logicOpEnable: false,
			logicOp: LogicOp.Copy,
			attachments: (target != null) ? target.ColorAttachments.Select(x => x.Blending ?? defaultBlending).ToArray() : [defaultBlending],
			blendConstants: default
		);

		using var dynamicState = new PipelineDynamicStateCreateInfo(
			next: default,
			flags: default,
			dynamicStates:
			[
				DynamicState.VertexInput,
				DynamicState.Scissor,
				DynamicState.Viewport,
			]
		);

		var shaderProgramData = AssetManager.GetShaderProgramData(shaderProgram);

		using var renderingInfo = new PipelineRenderingCreateInfo(
			next: default,
			viewMask: 0,
			colorAttachmentFormats: (target != null) ? target.ColorAttachments.Select(x => x.Texture.Format).ToArray() : [this.swapchainImageFormat],
			depthAttachmentFormat: (target != null) ? target.DepthAttachment?.Texture.Format ?? Format.Undefined : this.depthFormat,
			stencilAttachmentFormat: (target != null) ? target.StencilAttachment?.Texture.Format ?? Format.Undefined : Format.Undefined
		);

		using var graphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo(
			next: (nint)(&renderingInfo),
			flags: default,
			stages: shaderProgramData.Stages,
			vertexInputState: default,
			inputAssemblyState: inputAssembly,
			tessellationState: null,
			viewportState: viewport,
			rasterizationState: rasterization,
			multisampleState: multisample,
			depthStencilState: depthStencil,
			colorBlendState: colorBlend,
			dynamicState: dynamicState,
			layout: pipelineLayout,
			renderPass: null,
			subpass: 0,
			basePipeline: null,
			basePipelineIndex: -1
		);

		var pipeline = graphicsPipelineCreateInfo.CreateGraphicsPipeline(device, allocator);

		return pipeline;
	}

	private void StartRenderPass(IEnumerable<SceneObject> objects, uint swapchainImageIndex, RenderTarget? target = null)
	{
		var renderTargetData = (target != null) ? AssetManager.GetRenderTargetData(target) : null;
		var extent = (target != null) ? new Extent2D((uint)target.Width, (uint)target.Height) : this.swapchainExtent;

		using var dependencyInfoBegin = new DependencyInfo(
			next: default,
			dependencyFlags: default,
			memoryBarriers: null,
			bufferMemoryBarriers: null,
			imageMemoryBarriers: (renderTargetData != null) ? renderTargetData.BeginDependencies :
			[
				new ImageMemoryBarrier2(
					next: default,
					srcStage: PipelineStage2.None,
					srcAccess: Access2.None,
					dstStage: PipelineStage2.ColorAttachmentOutput,
					dstAccess: Access2.ColorAttachmentWrite,
					oldLayout: ImageLayout.Undefined,
					newLayout: ImageLayout.ColorAttachmentOptimal,
					srcQueueFamilyIndex: ~0u,
					dstQueueFamilyIndex: ~0u,
					image: swapchainImages[swapchainImageIndex],
					subresourceRange: new ImageSubresourceRange(ImageAspect.Color, 0, 1, 0, 1)
				),
				new ImageMemoryBarrier2(
					next: default,
					srcStage: PipelineStage2.None,
					srcAccess: Access2.None,
					dstStage: PipelineStage2.EarlyFragmentTests,
					dstAccess: Access2.DepthStencilAttachmentRead | Access2.DepthStencilAttachmentWrite,
					oldLayout: ImageLayout.Undefined,
					newLayout: ImageLayout.DepthAttachmentOptimal,
					srcQueueFamilyIndex: ~0u,
					dstQueueFamilyIndex: ~0u,
					image: depthImage,
					subresourceRange: new ImageSubresourceRange(ImageAspect.Depth, 0, 1, 0, 1)
				)
			]
		);

		var renderingInfo = (renderTargetData != null) ? renderTargetData.RenderingInfo : new RenderingInfo(
			next: default,
			flags: default,
			renderArea: new(offset: new(0, 0), extent: this.swapchainExtent),
			layerCount: 1,
			viewMask: 0,
			colorAttachments: [
				new RenderingAttachmentInfo(
					next: default,
					imageView: swapchainImageViews[swapchainImageIndex],
					imageLayout: ImageLayout.ColorAttachmentOptimal,
					resolveMode: ResolveMode.None,
					resolveImageView: null,
					resolveImageLayout: ImageLayout.Undefined,
					loadOp: AttachmentLoadOp.Clear,
					storeOp: AttachmentStoreOp.Store,
					clearValue: new(new ClearColorValue(0f, 0f, 0f, 0f))
				)
			],
			depthAttachment: new RenderingAttachmentInfo(
				next: default,
				imageView: depthImageView,
				imageLayout: ImageLayout.DepthAttachmentOptimal,
				resolveMode: ResolveMode.None,
				resolveImageView: null,
				resolveImageLayout: ImageLayout.Undefined,
				loadOp: AttachmentLoadOp.Clear,
				storeOp: AttachmentStoreOp.DontCare,
				clearValue: new(new ClearDepthStencilValue(depth: 0, stencil: 0))
			),
			stencilAttachment: null
		);

		using var globalDescriptorWrite = new WriteDescriptorSet(
			next: default,
			destinationSet: default,
			destinationBinding: GLOBAL_UNIFORMS_BINDING,
			destinationArrayElement: 0,
			descriptorType: DescriptorType.UniformBuffer,
			imageInfos: null,
			bufferInfos: [new(buffer: globalUniformsBuffers[currentFrame], offset: default, range: (ulong)Marshal.SizeOf<GlobalUniforms>())],
			texelBufferViews: null
		);

		var cmd = commandBuffers[currentFrame];

		cmd.PipelineBarrier2(dependencyInfoBegin);
		cmd.BeginRendering(renderingInfo);

		foreach (var obj in objects)
		{
			var material = obj.GetComponent<MeshRenderer>().Material;
			var mesh = obj.GetComponent<MeshFilter>().Mesh;
			var meshData = AssetManager.GetMeshData(mesh);

			if (!graphicsPipelines.TryGetValue((material.ShaderProgram, target), out var graphicsPipeline))
			{
				graphicsPipeline = CreateGraphicsPipeline(material.ShaderProgram, target);
				graphicsPipelines[(material.ShaderProgram, target)] = graphicsPipeline;
			}

			if (!vertexInputDescriptions.TryGetValue(mesh.VertexType, out var vertexInputDescription))
			{
				vertexInputDescription = CreateVertexInputDescriptions(mesh.VertexType);
				vertexInputDescriptions[mesh.VertexType] = vertexInputDescription;
			}

			cmd.BindPipeline(graphicsPipeline, PipelineBindPoint.Graphics);
			cmd.SetScissors(new Rect2D(offset: new(0, 0), extent: extent));
			cmd.SetViewports(new Viewport(x: 0, y: 0, width: extent.Width, height: extent.Height, minDepth: 0f, maxDepth: 1f));
			cmd.SetVertexInput(vertexInputDescription.Bindings, vertexInputDescription.Attributes);
			cmd.BindVertexBuffers(meshData.VertexBuffer);
			cmd.BindIndexBuffer(meshData.IndexBuffer, meshData.IndexType);
			cmd.PushDescriptorSet(PipelineBindPoint.Graphics, pipelineLayout, globalDescriptorWrite);

			var pushConstants = new PushConstants(obj.Model, (obj is UIObject uiObject) ? uiObject.Id : 0);
			cmd.PushConstants(pipelineLayout, ShaderStage.All, offset: 0, size: (uint)Marshal.SizeOf<PushConstants>(), ref Unsafe.As<PushConstants, byte>(ref pushConstants));

			CreateUniformsBuffer(material.Uniforms, out Buffer? uniformsBuffer, out DeviceMemory? uniformsMemory, out DeviceSize uniformsSize);
			bool hasUniforms = uniformsSize != 0;

			if (hasUniforms)
			{
				using var objectDescriptorWrite = new WriteDescriptorSet(
					next: default,
					destinationSet: default,
					destinationBinding: OBJECT_UNIFORMS_BINDING,
					destinationArrayElement: 0,
					descriptorType: DescriptorType.UniformBuffer,
					imageInfos: null,
					bufferInfos: [new(buffer: uniformsBuffer!, offset: default, range: uniformsSize)],
					texelBufferViews: null
				);

				cmd.PushDescriptorSet(PipelineBindPoint.Graphics, pipelineLayout, objectDescriptorWrite);

				toBeDisposed[currentFrame].Enqueue(uniformsBuffer!);
				toBeDisposed[currentFrame].Enqueue(uniformsMemory!);
			}

			var textures = material.Uniforms
				.OfType<Texture>()
				.Select(x => AssetManager.GetTextureData(x))
				.Index()
				.Select(x => new WriteDescriptorSet(
						next: default,
						destinationSet: default,
						destinationBinding: (uint)(TEXTURES_BINDING + x.Index),
						destinationArrayElement: 0,
						descriptorType: DescriptorType.CombinedImageSampler,
						imageInfos: [new DescriptorImageInfo(sampler: x.Item.Sampler, imageView: x.Item.ImageView, imageLayout: ImageLayout.ShaderReadOnlyOptimal)],
						bufferInfos: null,
						texelBufferViews: null
					)
				)
				.ToArray()
			;

			if (textures.Length > 0)
			{
				cmd.PushDescriptorSet(PipelineBindPoint.Graphics, pipelineLayout, textures);

				foreach (var x in textures)
					toBeDisposed[currentFrame].Enqueue(x);
			}

			cmd.DrawIndexed(mesh.IndexCount);
		}

		using var dependencyInfoEnd = new DependencyInfo(
			next: default,
			dependencyFlags: default,
			memoryBarriers: null,
			bufferMemoryBarriers: null,
			imageMemoryBarriers: (renderTargetData != null) ? renderTargetData.EndDependencies :
			[
				new ImageMemoryBarrier2(
					next: default,
					srcStage: PipelineStage2.ColorAttachmentOutput,
					srcAccess: Access2.ColorAttachmentWrite,
					dstStage: PipelineStage2.BottomOfPipe,
					dstAccess: Access2.None,
					oldLayout: ImageLayout.ColorAttachmentOptimal,
					newLayout: ImageLayout.PresentSrc,
					srcQueueFamilyIndex: ~0u,
					dstQueueFamilyIndex: ~0u,
					image: swapchainImages[swapchainImageIndex],
					subresourceRange: new ImageSubresourceRange(ImageAspect.Color, 0, 1, 0, 1)
				)
			]
		);

		cmd.EndRendering();
		cmd.PipelineBarrier2(dependencyInfoEnd);

		if (renderTargetData == null)
			renderingInfo.Dispose();
	}

	public void DrawFrame(Matrix4x4 projection, Matrix4x4 view, IEnumerable<SceneObject> objects, RenderTarget? target = null)
	{
		if (objects == null)
			throw new ArgumentNullException();

		inFlightFence[currentFrame].Wait();
		inFlightFence[currentFrame].Reset();

		while (toBeDisposed[currentFrame].Count > 0)
			toBeDisposed[currentFrame].Dequeue().Dispose();

		Marshal.StructureToPtr(new GlobalUniforms(view.Inversed, projection, view.t), globalUniformsLocations[currentFrame], false);

		uint imageIndex = (target == null) ? swapchain.GetNextImage(imageAvailableSemaphore[currentFrame]) : ~0u;

		var cmd = commandBuffers[currentFrame];

		using var beginInfo = new CommandBufferBeginInfo(
			next: default,
			usage: default,
			inheritanceInfo: null
		);

		cmd.Reset(default);
		cmd.Begin(beginInfo);
		StartRenderPass(objects, imageIndex, target);
		cmd.End();

		using var submitInfo = new SubmitInfo(
			next: default,
			waitSemaphores: (target == null) ? [imageAvailableSemaphore[currentFrame]] : null,
			waitDstStageMasks: (target == null) ? [PipelineStage.ColorAttachmentOutput] : null,
			commandBuffers: [cmd],
			signalSemaphores: (target == null) ? [renderFinishedSemaphore[imageIndex]] : null
		);

		graphicsQueue.Submit(inFlightFence[currentFrame], submitInfo);

		if (target == null)
		{
			using var presentInfo = new PresentInfo(
				next: default,
				waitSemaphores: [renderFinishedSemaphore[imageIndex]],
				swapchains: [swapchain],
				imageIndices: [imageIndex],
				results: null
			);

			presentationQueue.Present(presentInfo);
		}

		if (++currentFrame >= maxFrames)
			currentFrame = 0;
	}
}

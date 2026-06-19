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

public partial class Renderer
{
	private readonly Compiler shaderCompiler = new();
	private readonly List<(Shader Shader, ShaderModule Module, PipelineShaderStageCreateInfo Stage)> compiledShaders = new();
	private readonly Dictionary<Type, (VertexInputBindingDescription2[] Bindings, VertexInputAttributeDescription2[] Attributes)> vertexInputDescriptions = new();
	private readonly Dictionary<(RenderPass, ShaderProgram), Pipeline> graphicsPipelines = new();

	public ShaderProgram CreateShaderProgram(string[] filenames)
	{
		if (filenames == null)
			throw new ArgumentNullException();

		var shaders = new Shader[filenames.Length];
		var modules = new ShaderModule[filenames.Length];
		var stages = new PipelineShaderStageCreateInfo[filenames.Length];

		for (int i = 0; i < filenames.Length; i++)
		{
			string filename = Path.GetFullPath(filenames[i]);

			var query = compiledShaders.Where(x => x.Shader.File == filename);
			Shader shader;

			if (!query.Any())
			{
				shader = shaderCompiler.Compile(filename);

				using ShaderModuleCreateInfo shaderModuleCreateInfo = new(
					next: default,
					flags: default,
					code: shader.Code
				);

				var module = shaderModuleCreateInfo.CreateShaderModule(device, allocator);
				var stage = new PipelineShaderStageCreateInfo(
					next: default,
					flags: default,
					stage: shader.Stage,
					module: module,
					name: shader.EntryPoint,
					specializationInfo: null
				);

				compiledShaders.Add((shader, module, stage));
				(shaders[i], modules[i], stages[i]) = (shader, module, stage);
			}
			else
			{
				(shaders[i], modules[i], stages[i]) = query.First();
			}
		}

		return new(shaders, modules, stages);
	}

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
						Type t when t == typeof(Vulkan.Color) => Format.R32G32B32A32SFloat,
						Type t when t == typeof(Vulkan.Vector2) => Format.R32G32SFloat,
						Type t when t == typeof(Vulkan.Vector3) => Format.R32G32B32SFloat,
						Type t when t == typeof(Vulkan.Vector4) => Format.R32G32B32A32SFloat,
						Type t when t == typeof(Vulkan.Vector2Int) => Format.R32G32SInt,
						Type t when t == typeof(Vulkan.Vector3Int) => Format.R32G32B32SInt,
						Type t when t == typeof(Vulkan.Vector4Int) => Format.R32G32B32A32SInt,
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

	public Pipeline CreateGraphicsPipeline(RenderPass renderPass, ShaderProgram shaderProgram)
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
			viewports:
			[
				new(
					x: 0f,
					y: 0f,
					width: (float)extent.Width,
					height: (float)extent.Height,
					minDepth: 0f,
					maxDepth: 1f
				)
			],
			scissors:
			[
				new(
					offset: new(x: 0, y: 0),
					extent: extent
				)
			]
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

		using var colorBlend = new PipelineColorBlendStateCreateInfo(
			next: default,
			flags: default,
			logicOpEnable: false,
			logicOp: LogicOp.Copy,
			attachments:
			[
				new(
					blendEnable: !(shaderProgram.DisableBlending ?? false),
					srcColorBlendFactor: shaderProgram.SourceBlendFactor ?? BlendFactor.One,
					dstColorBlendFactor: shaderProgram.DestinationBlendFactor ?? BlendFactor.Zero,
					colorBlendOp: shaderProgram.BlendOp ?? BlendOp.Add,
					srcAlphaBlendFactor: shaderProgram.SourceBlendFactor ?? BlendFactor.One,
					dstAlphaBlendFactor: shaderProgram.DestinationBlendFactor ?? BlendFactor.Zero,
					alphaBlendOp: shaderProgram.BlendOp ?? BlendOp.Add,
					colorWriteMask: ColorComponent.R | ColorComponent.G | ColorComponent.B | ColorComponent.A
				)
			],
			blendConstants: default
		);

		using var dynamicState = new PipelineDynamicStateCreateInfo(
			next: default,
			flags: default,
			dynamicStates:
			[
				DynamicState.VertexInput,
			]
		);

		using var graphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo(
			next: default,
			flags: default,
			stages: shaderProgram.Stages,
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
			renderPass: renderPass,
			subpass: 0,
			basePipeline: null,
			basePipelineIndex: -1
		);

		var pipeline = graphicsPipelineCreateInfo.CreateGraphicsPipeline(device, allocator);

		return pipeline;
	}

	protected virtual void StartRenderPass(
		RenderPass renderPass,
		Framebuffer framebuffer,
		Extent2D extent,
		IEnumerable<SceneObject> objects
	)
	{
		using var renderPassInfo = new RenderPassBeginInfo(
			next: default,
			renderPass: renderPass,
			framebuffer: framebuffer,
			renderArea: new(offset: new(0, 0), extent: extent),
			clearValues:
			[
				new(
					color: new(float32: new(0, 0, 0, 0), int32: default, uint32: default),
					depthStencil: default
				),
				new(
					color: default,
					depthStencil: new(depth: 1f, stencil: 0)
				)
			]
		);

		using var globalDescriptorWrite = new WriteDescriptorSet(
			next: default,
			destinationSet: default,
			destinationBinding: 0,
			destinationArrayElement: 0,
			descriptorType: DescriptorType.UniformBuffer,
			imageInfos: null,
			bufferInfos: [new(buffer: globalUniformsBuffers[currentFrame], offset: default, range: (ulong)Marshal.SizeOf<GlobalUniforms>())],
			texelBufferViews: null
		);

		var cmd = commandBuffers[currentFrame];

		cmd.BeginRenderPass(renderPassInfo, SubpassContents.Inline);

		foreach (var obj in objects)
		{
			var material = obj.GetComponent<MeshRenderer>().Material;
			var mesh = obj.GetComponent<MeshFilter>().Mesh;

			if (!graphicsPipelines.TryGetValue((renderPass, material.ShaderProgram), out var graphicsPipeline))
			{
				graphicsPipeline = CreateGraphicsPipeline(renderPass, material.ShaderProgram);
				graphicsPipelines[(renderPass, material.ShaderProgram)] = graphicsPipeline;
			}

			if (!vertexInputDescriptions.TryGetValue(mesh.VertexType, out var vertexInputDescription))
			{
				vertexInputDescription = CreateVertexInputDescriptions(mesh.VertexType);
				vertexInputDescriptions[mesh.VertexType] = vertexInputDescription;
			}

			cmd.BindPipeline(graphicsPipeline, PipelineBindPoint.Graphics);
			cmd.SetVertexInput(vertexInputDescription.Bindings, vertexInputDescription.Attributes);
			cmd.BindVertexBuffers(mesh.VertexBuffer);
			cmd.BindIndexBuffer(mesh.IndexBuffer, mesh.IndexType);
			cmd.PushDescriptorSet(PipelineBindPoint.Graphics, pipelineLayout, globalDescriptorWrite);

			var pushConstants = new PushConstants(obj.Transform, (obj is UIObject uIObject) ? uIObject.Id : 0);
			cmd.PushConstants(pipelineLayout, ShaderStage.All, offset: 0, size: (uint)Marshal.SizeOf<PushConstants>(), ref Unsafe.As<PushConstants, byte>(ref pushConstants));

			var uniformsSize = CreateUniformsBuffer(material.Uniforms, out Buffer? uniformsBuffer, out DeviceMemory? uniformsMemory);
			bool hasUniforms = uniformsSize != 0;

			if (hasUniforms)
			{
				using var objectDescriptorWrite = new WriteDescriptorSet(
					next: default,
					destinationSet: default,
					destinationBinding: 1,
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
				.Select(x => x.Value)
				.OfType<Texture>()
				.Select(x => new DescriptorImageInfo(sampler: x.Sampler, imageView: x.ImageView, imageLayout: ImageLayout.ShaderReadOnlyOptimal))
				.ToArray()
			;

			if (textures.Length > 0)
			{
				using var texturesDescriptorWrite = new WriteDescriptorSet(
					next: default,
					destinationSet: default,
					destinationBinding: 2,
					destinationArrayElement: 0,
					descriptorType: DescriptorType.CombinedImageSampler,
					imageInfos: textures,
					bufferInfos: null,
					texelBufferViews: null
				);

				cmd.PushDescriptorSet(PipelineBindPoint.Graphics, pipelineLayout, texturesDescriptorWrite);
			}

			cmd.DrawIndexed(mesh.IndexCount);
		}

		cmd.EndRenderPass();
	}

	// if throws ErrorOutOfDateKhr or SuboptimalKhr it needs swapchain recreation (see https://vulkan-tutorial.com/en/Drawing_a_triangle/Swap_chain_recreation)
	public virtual void DrawFrame(Matrix4x4 projection, Matrix4x4 view, IEnumerable<SceneObject> objects, RenderTexture? texture = null)
	{
		if (objects == null)
			throw new ArgumentNullException();

		inFlightFence[currentFrame].Wait();
		inFlightFence[currentFrame].Reset();

		while (toBeDisposed[currentFrame].Count > 0)
			toBeDisposed[currentFrame].Dequeue().Dispose();

		Marshal.StructureToPtr(new GlobalUniforms(view.Inverse, projection, view.t.xyz), globalUniformsLocations[currentFrame], false);

		uint imageIndex = (texture == null) ? swapchain.GetNextImage(imageAvailableSemaphore[currentFrame]) : ~0u;

		var cmd = commandBuffers[currentFrame];

		using var beginInfo = new CommandBufferBeginInfo(
			next: default,
			usage: default,
			inheritanceInfo: null
		);

		cmd.Reset(default);
		cmd.Begin(beginInfo);
		if (texture is RenderTexture rt)
		{
			StartRenderPass(rt.RenderPass, rt.Framebuffer, rt.Extent, objects);
			TransitionImageLayout(rt.Image, ImageLayout.PresentSrc, ImageLayout.ShaderReadOnlyOptimal, ImageAspect.Color, cmd);
		}
		else StartRenderPass(renderPass, framebuffers[imageIndex], extent, objects);
		cmd.End();

		using var submitInfo = new SubmitInfo(
			next: default,
			waitSemaphores: (texture == null) ? [imageAvailableSemaphore[currentFrame]] : null,
			waitDstStageMasks: (texture == null) ? [PipelineStage.ColorAttachmentOutput] : null,
			commandBuffers: [cmd],
			signalSemaphores: (texture == null) ? [renderFinishedSemaphore[imageIndex]] : null
		);

		graphicsQueue.Submit(inFlightFence[currentFrame], submitInfo);

		if (texture == null)
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

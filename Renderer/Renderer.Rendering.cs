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

namespace Renderer;

public partial class Renderer
{
	protected Dictionary<string, (ShaderInfo, byte[])> shaderCode = new();
	protected Dictionary<RenderInfo, Pipeline> graphicsPipelines = new();

	protected ShaderModule CreateShaderModule(ref ShaderInfo info)
	{
		if (info == null) throw new ArgumentNullException();

		string path = Path.GetFullPath(info.File);
		byte[]? code;

		if (!shaderCode.ContainsKey(path))
		{
			code = Compiler.Compile(ref info);
			shaderCode.Add(path, (info, code!));
		}
		else (info, code) = shaderCode[path];

		using ShaderModuleCreateInfo shaderModuleCreateInfo = new(
			next: default,
			flags: default,
			code: code!
		);

		return shaderModuleCreateInfo.CreateShaderModule(device, allocator);
	}

	protected virtual Pipeline CreateGraphicsPipeline(RenderInfo obj, RenderPass renderPass)
	{
		var modules = new ShaderModule[obj.Shaders.Length];

		var stages = obj.Shaders.Index().Select(x =>
			{
				(int i, ShaderInfo info) = x;

				modules[i] = CreateShaderModule(ref info);
				obj.Shaders[i] = info;

				return new PipelineShaderStageCreateInfo(
					next: default,
					flags: default,
					stage: (ShaderStage)info.Stage!,
					module: modules[i],
					name: info.EntryPoint!,
					specializationInfo: null
				);
			}
		).ToArray();

		using var vertexInput = new PipelineVertexInputStateCreateInfo(
			next: default,
			flags: default,
			vertexBindingDescriptions: obj.BindingDescriptions,
			vertexAttributeDescriptions: obj.AttributeDescriptions
		);

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
			cullMode: obj.CullMode ?? CullMode.Back,
			frontFace: obj.FrontFace ?? FrontFace.CounterClockwise,
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

		var srcFactor = obj.SourceBlendFactor ?? BlendFactor.One;
		var destFactor = obj.DestinationBlendFactor ?? BlendFactor.Zero;
		var blendOp = obj.BlendOp ?? BlendOp.Add;

		using var colorBlend = new PipelineColorBlendStateCreateInfo(
			next: default,
			flags: default,
			logicOpEnable: false,
			logicOp: LogicOp.Copy,
			attachments:
			[
				new(
					blendEnable: !(obj.DisableBlending ?? false),
					srcColorBlendFactor: srcFactor,
					dstColorBlendFactor: destFactor,
					colorBlendOp: blendOp,
					srcAlphaBlendFactor: srcFactor,
					dstAlphaBlendFactor: destFactor,
					alphaBlendOp: blendOp,
					colorWriteMask: ColorComponent.R | ColorComponent.G | ColorComponent.B | ColorComponent.A
				)
			],
			blendConstants: default
		);

		using var dynamicState = new PipelineDynamicStateCreateInfo(
			next: default,
			flags: default,
			dynamicStates: null
		);

		using var graphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo(
			next: default,
			flags: default,
			stages: stages,
			vertexInputState: vertexInput,
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

		foreach (var x in stages)
			x.Dispose();

		foreach (var x in modules)
			x.Dispose();

		return pipeline;
	}

	protected virtual void StartRenderPass(
		RenderPass renderPass,
		Framebuffer framebuffer,
		Extent2D extent,
		Vector3 cameraPosition,
		IEnumerable<IRenderable> objects
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
			var model = obj.Model;
			var uniforms = obj.Uniforms;
			var info = obj.Info;
			var id = obj.Id;

			Pipeline graphicsPipeline;

			if (!graphicsPipelines.TryGetValue(info, out graphicsPipeline!))
			{
				graphicsPipeline = CreateGraphicsPipeline(info, renderPass);
				graphicsPipelines[info] = graphicsPipeline;
			}

			cmd.BindPipeline(graphicsPipeline, PipelineBindPoint.Graphics);
			cmd.BindVertexBuffers(info.VertexBuffer);
			cmd.BindIndexBuffer(info.IndexBuffer, info.IndexType);
			cmd.PushDescriptorSet(PipelineBindPoint.Graphics, pipelineLayout, globalDescriptorWrite);

			cmd.PushConstants(pipelineLayout, ShaderStage.All, offset: 0, size: 64, ref Unsafe.As<Matrix4x4, byte>(ref model));
			cmd.PushConstants(pipelineLayout, ShaderStage.All, offset: 64, size: 12, ref Unsafe.As<Vector3, byte>(ref cameraPosition));
			cmd.PushConstants(pipelineLayout, ShaderStage.All, offset: 76, size: 4, ref Unsafe.As<uint, byte>(ref id));

			var uniformsSize = CreateUniformsBuffer(uniforms, out Buffer? uniformsBuffer, out DeviceMemory? uniformsMemory);
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

			var textures = uniforms
				.Select(x => x.Value)
				.OfType<TextureInfo>()
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

			cmd.DrawIndexed(info.IndexCount);
		}

		cmd.EndRenderPass();
	}

	// if throws ErrorOutOfDateKhr or SuboptimalKhr it needs swapchain recreation (see https://vulkan-tutorial.com/en/Drawing_a_triangle/Swap_chain_recreation)
	public virtual void DrawFrame(Matrix4x4 projection, Matrix4x4 view, IEnumerable<IRenderable> objects, RenderTextureInfo? texture = null)
	{
		if (objects == null)
			throw new ArgumentNullException();

		inFlightFence[currentFrame].Wait();
		inFlightFence[currentFrame].Reset();

		while (toBeDisposed[currentFrame].Count > 0)
			toBeDisposed[currentFrame].Dequeue().Dispose();

		Marshal.StructureToPtr(new GlobalUniforms(view.Inverse, projection), globalUniformsLocations[currentFrame], false);

		uint imageIndex = (texture == null) ? swapchain.GetNextImage(imageAvailableSemaphore[currentFrame]) : ~0u;

		var cmd = commandBuffers[currentFrame];
		var cameraPosition = view.t.xyz;

		using var beginInfo = new CommandBufferBeginInfo(
			next: default,
			usage: default,
			inheritanceInfo: null
		);

		cmd.Reset(default);
		cmd.Begin(beginInfo);
		if (texture is RenderTextureInfo rt)
		{
			StartRenderPass(rt.RenderPass, rt.Framebuffer, rt.Extent, cameraPosition, objects);
			TransitionImageLayout(rt.Image, ImageLayout.PresentSrc, ImageLayout.ShaderReadOnlyOptimal, ImageAspect.Color, cmd);
		}
		else StartRenderPass(renderPass, framebuffers[imageIndex], extent, cameraPosition, objects);
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

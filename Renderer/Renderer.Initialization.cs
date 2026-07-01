using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using Vulkan;

using Buffer = Vulkan.Buffer;

namespace Renderer;

internal sealed partial class Renderer : IDisposable
{
	private readonly GLFW.Window window;
	private readonly AllocationCallbacks? allocator;

	private Queue<IDisposable>[] toBeDisposed;
	private uint graphicsQueueFamilyIndex, presentationQueueFamilyIndex;
	private Format swapchainImageFormat, depthFormat;
	private Extent2D extent;

	private uint currentFrame = 0;
	private uint maxFrames => (uint)swapchainImageViews.Length;

	private Instance instance;
	private DebugUtilsMessenger debugUtilsMessenger;
	private PhysicalDevice physicalDevice;
	private Device device;
	private Swapchain swapchain;
	private Image[] swapchainImages;
	private ImageView[] swapchainImageViews;
	private PipelineLayout pipelineLayout;
	private CommandPool commandPool;
	private CommandBuffer[] commandBuffers;
	private Semaphore[] imageAvailableSemaphore, renderFinishedSemaphore;
	private Fence[] inFlightFence;
	private Queue graphicsQueue, presentationQueue;
	private DescriptorSetLayout[] descriptorSetLayouts;
	private Buffer[] globalUniformsBuffers;
	private DeviceMemory[] globalUniformsMemories;
	private nint[] globalUniformsLocations;
	private Image depthImage;
	private ImageView depthImageView;
	private DeviceMemory depthImageMemory;

	public AllocationCallbacks? Allocator => allocator;
	public Instance Instance => instance ?? throw new NullReferenceException("Instance has not been initialized.");
	public PhysicalDevice PhysicalDevice => physicalDevice ?? throw new NullReferenceException("PhysicalDevice has not been initialized.");
	public Device Device => device ?? throw new NullReferenceException("Device has not been initialized.");

	public AssetManager AssetManager { get; }

	public static uint MakeVersion(int major, int minor, int patch) => ((((uint)major) << 22) | (((uint)minor) << 12) | ((uint)patch));
	public static uint MakeApiVersion(int variant, int major, int minor, int patch) => ((((uint)variant) << 29) | (((uint)major) << 22) | (((uint)minor) << 12) | ((uint)patch));

	private readonly DebugUtilsMessengerCallback debugUtilsMessengerCallback;
	public event EventHandler<DebugUtilsMessengerEventArgs>? DebugUtilsMessageReceived;

	public uint FindMemoryType(uint typeFilter, MemoryProperty properties)
	{
		if (physicalDevice == default)
			throw new NullReferenceException("Physical device has not been initialized.");

		var memProperties = physicalDevice.MemoryProperties;
		int i = 0;

		foreach (var x in memProperties.MemoryTypes)
		{
			if ((typeFilter & (1 << i)) != 0 && x.Properties.HasFlag(properties))
				return (uint)i;

			i++;
		}

		throw new VulkanException("Failed to find suitable memory type.");
	}

	public Format FindSupportedFormat(Format[] candidates, ImageTiling tiling, FormatFeatures features)
	{
		if (candidates == null)
			throw new ArgumentNullException();

		if (physicalDevice == default)
			throw new NullReferenceException("Physical device has not been initialized.");

		foreach (var format in candidates)
		{
			var properties = physicalDevice.GetFormatProperties(format);

			if (tiling == ImageTiling.Linear && properties.LinearTilingFeatures.HasFlag(features))
				return format;

			if (tiling == ImageTiling.Optimal && properties.OptimalTilingFeatures.HasFlag(features))
				return format;
		}

		throw new NullReferenceException("Failed to find supported format.");
	}

	private void InitializeInstance()
	{
		using var appInfo = new ApplicationInfo(
			next: default,
			applicationName: "Vulkan Test",
			applicationVersion: MakeVersion(1, 0, 0),
			engineName: "No Engine",
			engineVersion: MakeVersion(1, 0, 0),
			apiVersion: MakeApiVersion(0, 1, 4, 0)
		);

		var extensions = new List<string>(GLFW.Program.RequiredInstanceExtensions!);
		extensions.Add("VK_KHR_portability_enumeration");
		extensions.Add("VK_EXT_debug_utils");
		extensions.Add("VK_KHR_get_physical_device_properties2");

		using var instanceCreateInfo = new InstanceCreateInfo(
			next: default,
			flags: InstanceCreateFlags.NumeratePortability,
			applicationInfo: appInfo,
			enabledLayerNames: ["VK_LAYER_KHRONOS_validation"],
			enabledExtensionNames: extensions.ToArray()
		);

		instance = instanceCreateInfo.CreateInstance(allocator);
	}

	private void InitializeDebugUtilsMessenger()
	{
		var debugUtilsMessengerCreateInfo = new DebugUtilsMessengerCreateInfo(
			next: default,
			flags: default,
			messageSeverity: DebugUtilsMessageSeverity.Info | DebugUtilsMessageSeverity.Verbose | DebugUtilsMessageSeverity.Warning | DebugUtilsMessageSeverity.Error,
			messageType: DebugUtilsMessageType.General | DebugUtilsMessageType.Validation | DebugUtilsMessageType.Performance,
			userCallback: debugUtilsMessengerCallback,
			userData: default
		);

		debugUtilsMessenger = debugUtilsMessengerCreateInfo.CreateDebugUtilsMessanger(instance, allocator);
	}

	private void InitializePhysicalDevice() =>
		physicalDevice = instance.PhysicalDevices.Where(x => x.Properties.DeviceType == PhysicalDeviceType.IntegratedGpu).First()
	;

	private unsafe void InitializeDevice()
	{
		graphicsQueueFamilyIndex = find(physicalDevice.QueueFamilyProperties, static (i, x) => x.QueueFlags.HasFlag(QueueFlags.Graphics));
		presentationQueueFamilyIndex = find(physicalDevice.QueueFamilyProperties, (i, x) => instance.Surface!.IsSupported(physicalDevice, (uint)i));

		using var graphicsDeviceQueueCreateInfo = new DeviceQueueCreateInfo(
			next: default,
			flags: default,
			queueFamilyIndex: graphicsQueueFamilyIndex,
			queuePriorities: [1f]
		);

		using var presentationDeviceQueueCreateInfo = new DeviceQueueCreateInfo(
			next: default,
			flags: default,
			queueFamilyIndex: presentationQueueFamilyIndex,
			queuePriorities: [1f]
		);

		var extendedDynamicStateFeatures = new PhysicalDeviceExtendedDynamicStateFeatures(
			next: default,
			extendedDynamicState: true
		);

		var indexTypeUInt8Features = new PhysicalDeviceIndexTypeUInt8Features(
			next: (nint)(&extendedDynamicStateFeatures),
			indexTypeUInt8: true
		);

		var vertexInputDynamicStateFeatures = new PhysicalDeviceVertexInputDynamicStateFeatures(
			next: (nint)(&indexTypeUInt8Features),
			vertexInputDynamicState: true
		);

		var extendedDynamicState3Features = new PhysicalDeviceExtendedDynamicState3Features(

			next: (nint)(&vertexInputDynamicStateFeatures),
			tessellationDomainOrigin: false,
			depthClampEnable: false,
			polygonMode: false,
			rasterizationSamples: false,
			sampleMask: false,
			alphaToCoverageEnable: false,
			alphaToOneEnable: false,
			logicOpEnable: false,
			colorBlendEnable: true,
			colorBlendEquation: true,
			colorWriteMask: false,
			rasterizationStream: false,
			conservativeRasterizationMode: false,
			extraPrimitiveOverestimationSize: false,
			depthClipEnable: false,
			sampleLocationsEnable: false,
			colorBlendAdvanced: false,
			provokingVertexMode: false,
			lineRasterizationMode: false,
			lineStippleEnable: false,
			depthClipNegativeOneToOne: false,
			viewportWScalingEnable: false,
			viewportSwizzle: false,
			coverageToColorEnable: false,
			coverageToColorLocation: false,
			coverageModulationMode: false,
			coverageModulationTableEnable: false,
			coverageModulationTable: false,
			coverageReductionMode: false,
			representativeFragmentTestEnable: false,
			shadingRateImageEnable: false
		);

		var dynamicRenderingFeatures = new PhysicalDeviceDynamicRenderingFeatures(
			next: (nint)(&extendedDynamicState3Features),
			dynamicRendering: true
		);

		var synchronization2Features = new PhysicalDeviceSynchronization2Features(
			next: (nint)(&dynamicRenderingFeatures),
			synchronization2: true
		);

		using var deviceCreateInfo = new DeviceCreateInfo(
			next: (nint)(&synchronization2Features),
			flags: default,
			queueCreateInfos: (graphicsQueueFamilyIndex != presentationQueueFamilyIndex) ? [graphicsDeviceQueueCreateInfo, presentationDeviceQueueCreateInfo] : [graphicsDeviceQueueCreateInfo],
			enabledLayerNames: null,
			enabledExtensionNames:
			[
				"VK_KHR_swapchain",
				"VK_EXT_vertex_input_dynamic_state",
				"VK_EXT_index_type_uint8",
				"VK_EXT_extended_dynamic_state",
				"VK_EXT_extended_dynamic_state3",
				"VK_KHR_push_descriptor",
				"VK_KHR_dynamic_rendering",
				"VK_KHR_synchronization2",
			],
			enabledFeatures: physicalDevice.Features
		);

		device = deviceCreateInfo.CreateDevice(physicalDevice, allocator);

		static uint find(QueueFamilyProperties[] properties, Func<int, QueueFamilyProperties, bool> predicate) =>
			(uint)properties
			.Index()
			.Where(x =>
			{
				(int i, QueueFamilyProperties q) = x;
				return predicate(i, q);
			})
			.Select(x =>
			{
				(int i, QueueFamilyProperties q) = x;
				return i;
			})
			.First()
		;
	}

	private void InitializeSwapchain()
	{
		(int framebufferWidth, int framebufferHeight) = window.FramebufferSize;

		SwapchainProperties swapchainProperties = new(physicalDevice, instance.Surface!);
		SurfaceFormat surfaceFormat = swapchainProperties.GetSurfaceFormat(Format.R8G8B8A8SRGB, ColorSpace.SRGBNonlinear);
		PresentMode presentMode = swapchainProperties.GetPresentMode(PresentMode.Mailbox);
		uint imageCount = swapchainProperties.Capabilities.MinImageCount + 1;

		/*Extent2D*/
		extent = swapchainProperties.GetExtent(framebufferWidth, framebufferHeight);
		swapchainImageFormat = surfaceFormat.Format;

		if (swapchainProperties.Capabilities.MaxImageCount > 0 && imageCount > swapchainProperties.Capabilities.MaxImageCount)
			imageCount = swapchainProperties.Capabilities.MaxImageCount;

		using var swapchainCreateInfo = new SwapchainCreateInfo(
			next: default,
			flags: default,
			surface: instance.Surface!,
			minImageCount: imageCount,
			imageFormat: swapchainImageFormat,
			imageColorSpace: surfaceFormat.ColorSpace,
			imageExtent: extent,
			imageArrayLayers: 1,
			imageUsage: ImageUsage.ColorAttachment,
			imageSharingMode: (graphicsQueueFamilyIndex != presentationQueueFamilyIndex) ? SharingMode.Concurrent : SharingMode.Exclusive,
			queueFamilyIndices: (graphicsQueueFamilyIndex != presentationQueueFamilyIndex) ? [graphicsQueueFamilyIndex, presentationQueueFamilyIndex] : [graphicsQueueFamilyIndex],
			preTransform: swapchainProperties.Capabilities.CurrentTransform,
			compositeAlpha: CompositeAlphaFlags.Opaque,
			presentMode: presentMode,
			clipped: true,
			oldSwapchain: default
		);

		swapchain = swapchainCreateInfo.CreateSwapchain(device, allocator);
	}

	private void InitializeImageViews()
	{
		swapchainImages = swapchain.GetImages();
		swapchainImageViews = new ImageView[swapchainImages.Length];

		for (int i = 0; i < swapchainImageViews.Length; i++)
			CreateImageView(swapchainImages[i], swapchainImageFormat, ImageAspect.Color, ImageViewType.Generic2D, out swapchainImageViews[i]);
	}

	private void InitializeDescriptorSetLayout()
	{
		var globalUniformsBinding = new DescriptorSetLayoutBinding(
			binding: 0,
			descriptorType: DescriptorType.UniformBuffer,
			descriptorCount: 1,
			stage: ShaderStage.AllGraphics,
			immutableSamplers: null
		);

		var objectUniformsBinding = new DescriptorSetLayoutBinding(
			binding: 1,
			descriptorType: DescriptorType.UniformBuffer,
			descriptorCount: 1,
			stage: ShaderStage.AllGraphics,
			immutableSamplers: null
		);

		var samplersBinding = new DescriptorSetLayoutBinding(
			binding: 2,
			descriptorType: DescriptorType.CombinedImageSampler,
			descriptorCount: 1,
			stage: ShaderStage.AllGraphics,
			immutableSamplers: null
		);

		using var descriptorSetLayoutCreateInfo = new DescriptorSetLayoutCreateInfo(
			next: default,
			flags: DescriptorSetLayoutCreateFlags.PushDescriptor,
			bindings: [globalUniformsBinding, objectUniformsBinding, samplersBinding]
		);

		descriptorSetLayouts = new DescriptorSetLayout[maxFrames];

		for (int i = 0; i < maxFrames; i++)
			descriptorSetLayouts[i] = descriptorSetLayoutCreateInfo.CreateDescriptorSetLayout(device, allocator);
	}

	private void InitializeGlobalUniforms()
	{
		DeviceSize size = (ulong)Marshal.SizeOf<GlobalUniforms>();

		globalUniformsBuffers = new Buffer[maxFrames];
		globalUniformsMemories = new DeviceMemory[maxFrames];
		globalUniformsLocations = new nint[maxFrames];

		for (int i = 0; i < maxFrames; i++)
		{
			CreateBuffer(size, BufferUsage.UniformBuffer, out Buffer buffer);
			CreateBufferMemory(buffer, MemoryProperty.HostVisible | MemoryProperty.HostCoherent, out DeviceMemory memory);

			globalUniformsBuffers[i] = buffer;
			globalUniformsMemories[i] = memory;
			globalUniformsLocations[i] = memory.Map(size: size, offset: default, flags: default);
		}
	}

	private void InitializePipelineLayout()
	{
		using var pipelineLayoutCreateInfo = new PipelineLayoutCreateInfo(
			next: default,
			flags: default,
			setLayouts: [descriptorSetLayouts[0]],
			pushConstantRanges: [new(stage: ShaderStage.All, offset: 0, size: physicalDevice.Properties.Limits.MaxPushConstantsSize)]
		);

		pipelineLayout = pipelineLayoutCreateInfo.CreatePipelineLayout(device, allocator);
	}

	private void InitializeDepthImage()
	{
		depthFormat = FindSupportedFormat(
			[Format.D32SFloat, Format.D32SFloatS8UInt, Format.D24UNormS8UInt],
			ImageTiling.Optimal,
			FormatFeatures.DepthStencilAttachment
		);

		using var imageCreateInfo = new ImageCreateInfo(
			next: default,
			flags: default,
			imageType: ImageType.Generic2D,
			format: depthFormat,
			extent: new(extent.Width, extent.Height, 1),
			mipLevels: 1,
			arrayLayers: 1,
			samples: SampleCount.Bit1,
			tiling: ImageTiling.Optimal,
			usage: ImageUsage.DepthStencilAttachment | ImageUsage.Sampled,
			sharingMode: SharingMode.Exclusive,
			queueFamilyIndices: null,
			initialLayout: ImageLayout.DepthAttachmentOptimal
		);

		depthImage = imageCreateInfo.CreateImage(device, allocator);

		var memoryRequirements = depthImage.MemoryRequirements;
		var allocateInfo = new MemoryAllocateInfo(
			next: default,
			allocationSize: memoryRequirements.Size,
			memoryTypeIndex: FindMemoryType(memoryRequirements.MemoryType, MemoryProperty.DeviceLocal)
		);

		depthImageMemory = allocateInfo.CreateDeviceMemory(device, allocator);
		depthImageMemory.Bind(depthImage);

		var imageViewCreateInfo = new ImageViewCreateInfo(
			next: default,
			flags: default,
			image: depthImage,
			viewType: ImageViewType.Generic2D,
			format: depthFormat,
			components: default,
			subresourceRange: new(
				aspect: ImageAspect.Depth,
				baseMipLevel: 0,
				levelCount: 1,
				baseArrayLayer: 0,
				layerCount: 1
			)
		);

		depthImageView = imageViewCreateInfo.CreateImageView(device, allocator);
	}

	private void InitializeCommandPool()
	{
		var commandPoolCreateInfo = new CommandPoolCreateInfo(
			next: default,
			flags: CommandPoolCreateFlags.ResetCommandBuffer,
			queueFamilyIndex: graphicsQueueFamilyIndex
		);

		commandPool = commandPoolCreateInfo.CreateCommandPool(device, allocator);
	}

	private void InitializeCommandBuffers()
	{
		var commandBufferAllocateInfo = new CommandBufferAllocateInfo(
			next: default,
			commandPool: commandPool,
			level: CommandBufferLevel.Primary,
			commandBufferCount: maxFrames
		);

		commandBuffers = commandBufferAllocateInfo.CreateCommandBuffers(device, commandPool);
	}

	private void InitializeSyncObjects()
	{
		var semaphoreCreateInfo = new SemaphoreCreateInfo(
			next: default,
			flags: default
		);

		var fenceCreateInfo = new FenceCreateInfo(
			next: default,
			flags: FenceCreateFlags.Signaled
		);

		imageAvailableSemaphore = new Semaphore[maxFrames];
		renderFinishedSemaphore = new Semaphore[maxFrames];
		inFlightFence = new Fence[maxFrames];

		for (int i = 0; i < maxFrames; i++)
		{
			imageAvailableSemaphore[i] = semaphoreCreateInfo.CreateSemaphore(device, allocator);
			renderFinishedSemaphore[i] = semaphoreCreateInfo.CreateSemaphore(device, allocator);
			inFlightFence[i] = fenceCreateInfo.CreateFence(device, allocator);
		}
	}

	public void DeviceWaitIdle() => device.WaitIdle();

	public void RecreateSwapchain()
	{
		(int framebufferWidth, int framebufferHeight) = window.FramebufferSize;
		while (framebufferWidth == 0 || framebufferHeight == 0)
		{
			(framebufferWidth, framebufferHeight) = window.FramebufferSize;
			GLFW.Input.WaitForEvents();
		}

		DeviceWaitIdle();

		foreach (var x in swapchainImageViews)
			x.Dispose();

		swapchain.Dispose();

		InitializeSwapchain();
		InitializeImageViews();
	}

	public void Initialize()
	{
		InitializeInstance();
		InitializeDebugUtilsMessenger();

		instance.CreateSurface(window);

		InitializePhysicalDevice();
		InitializeDevice();
		InitializeSwapchain();
		InitializeImageViews();
		InitializeDescriptorSetLayout();
		InitializeGlobalUniforms();
		InitializePipelineLayout();
		InitializeDepthImage();
		InitializeCommandPool();
		InitializeCommandBuffers();
		InitializeSyncObjects();

		toBeDisposed = new Queue<IDisposable>[maxFrames];
		for (int i = 0; i < maxFrames; i++)
			toBeDisposed[i] = new();

		graphicsQueue = device.GetQueue(graphicsQueueFamilyIndex, 0);
		presentationQueue = device.GetQueue(presentationQueueFamilyIndex, 0);

		Console.WriteLine("Vulkan Initialized!");
	}

	public void Dispose()
	{
		AssetManager.Dispose();

		depthImageView.Dispose();
		depthImage.Dispose();
		depthImageMemory.Dispose();

		foreach (var x in toBeDisposed)
			while (x.Count > 0)
				x.Dequeue().Dispose();

		foreach (var x in globalUniformsMemories)
			x.Unmap();

		foreach (var x in imageAvailableSemaphore)
			x.Dispose();

		foreach (var x in renderFinishedSemaphore)
			x.Dispose();

		foreach (var x in inFlightFence)
			x.Dispose();

		commandPool.Dispose();
		pipelineLayout.Dispose();

		foreach (var x in graphicsPipelines.Values)
			x.Dispose();

		foreach (var x in swapchainImageViews)
			x.Dispose();

		swapchain.Dispose();

		foreach (var x in globalUniformsBuffers)
			x.Dispose();
		foreach (var x in globalUniformsMemories)
			x.Dispose();

		foreach (var x in descriptorSetLayouts)
			x.Dispose();

		device.Dispose();
		debugUtilsMessenger.Dispose();
		instance.Dispose();
	}

#pragma warning disable CS8618
	public Renderer(GLFW.Window window, AllocationCallbacks? allocator = null)
	{
		this.window = window;
		this.allocator = allocator;

		this.debugUtilsMessengerCallback = (DebugUtilsMessageSeverity severity, DebugUtilsMessageType type, in DebugUtilsMessengerCallbackData data, nint userData) =>
		{
			DebugUtilsMessageReceived?.Invoke(this, new(severity, type, data.Message, data.MessageIdName, data.MessageIdNumber, data.QueueLabels, data.CommandBufferLabels, data.Objects, userData));
			return false;
		};

		this.AssetManager = new(this);
	}
#pragma warning restore
}

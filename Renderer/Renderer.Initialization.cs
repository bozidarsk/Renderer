using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;

using Vulkan;

using VkBuffer = Vulkan.Buffer;

namespace Renderer;

internal sealed partial class Renderer : IDisposable
{
	private readonly GLFW.Window window;
	private readonly AllocationCallbacks? allocator;

	private List<IDisposable>[] toBeDisposed;
	private Format swapchainImageFormat, depthFormat;
	private Extent2D swapchainExtent;

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
	private DescriptorSetLayout descriptorSetLayout;
	private VkBuffer[] globalUniformsBuffers;
	private DeviceMemory[] globalUniformsMemories;
	private nint[] globalUniformsLocations;
	private Image depthImage;
	private ImageView depthImageView;
	private DeviceMemory depthImageMemory;

	private Fence[] imageAvailableFence;
	private ulong[] inFlightTimelineValues;

	private QueueContext graphicsQueueContext, presentationQueueContext, computeQueueContext, transferQueueContext;

	private readonly Lock disposingLock = new();

	public AllocationCallbacks? Allocator => allocator;
	public Instance Instance => instance ?? throw new NullReferenceException("Instance has not been initialized.");
	public PhysicalDevice PhysicalDevice => physicalDevice ?? throw new NullReferenceException("PhysicalDevice has not been initialized.");
	public Device Device => device ?? throw new NullReferenceException("Device has not been initialized.");
	public Extent2D SwapchainExtent => swapchainExtent;

	public QueueContext GraphicsQueueContext => graphicsQueueContext ?? throw new NullReferenceException("GraphicsQueueContext has not been initialized.");
	public QueueContext PresentationQueueContext => presentationQueueContext ?? throw new NullReferenceException("PresentationQueueContext has not been initialized.");
	public QueueContext ComputeQueueContext => computeQueueContext ?? throw new NullReferenceException("ComputeQueueContext has not been initialized.");
	public QueueContext TransferQueueContext => transferQueueContext ?? throw new NullReferenceException("TransferQueueContext has not been initialized.");

	public static readonly int MAX_TEXTURES = int.TryParse(Environment.GetEnvironmentVariable("VK_MAX_TEXTURES"), out int value) ? value : 16;
	public static readonly int MAX_BUFFERS = int.TryParse(Environment.GetEnvironmentVariable("VK_MAX_BUFFERS"), out int value) ? value : 16;
	public static readonly int GLOBAL_UNIFORMS_BINDING = 0;
	public static readonly int OBJECT_UNIFORMS_BINDING = 1;
	public static readonly int TEXTURES_BINDING = 2;
	public static readonly int BUFFERS_BINDING = 2 + MAX_TEXTURES;

	private static readonly Lock currentLock = new();

	public static Renderer? Current
	{
		set
		{
			lock (currentLock)
				field = value;
		}
		get;
	}

	public static Renderer Require() => Current ?? throw new InvalidOperationException("There is no current renderer.");

	public static uint MakeVersion(int major, int minor, int patch) => ((((uint)major) << 22) | (((uint)minor) << 12) | ((uint)patch));
	public static uint MakeApiVersion(int variant, int major, int minor, int patch) => ((((uint)variant) << 29) | (((uint)major) << 22) | (((uint)minor) << 12) | ((uint)patch));

	private readonly DebugUtilsMessengerCallback debugUtilsMessengerCallback;
	public event EventHandler<DebugUtilsMessengerEventArgs>? DebugUtilsMessageReceived;

	public readonly List<WeakReference> Assets = [];

	public void ToBeDisposed(IDisposable disposable)
	{
		lock (disposingLock)
			toBeDisposed[currentFrame].Add(disposable);
	}

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
			apiVersion: MakeApiVersion(0, 1, 3, 0)
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

	private void InitializePhysicalDevice()
	{
		var devices = instance.PhysicalDevices;

		if (uint.TryParse(Environment.GetEnvironmentVariable("VK_PHYSICAL_DEVICE_DEVICE_ID"), out var deviceId))
		{
			PhysicalDevice? foundDevice = devices.FirstOrDefault(x => x.Properties.DeviceID == deviceId);

			if (foundDevice != null)
				physicalDevice = foundDevice;
		}
		else if (uint.TryParse(Environment.GetEnvironmentVariable("VK_PHYSICAL_DEVICE_VENDOR_ID"), out var vendorId))
		{
			PhysicalDevice? foundDevice = devices.FirstOrDefault(x => x.Properties.VendorID == vendorId);

			if (foundDevice != null)
				physicalDevice = foundDevice;
		}
		else if (Enum.TryParse<PhysicalDeviceType>(Environment.GetEnvironmentVariable("VK_PHYSICAL_DEVICE_TYPE"), true, out var type))
		{
			PhysicalDevice? foundDevice = devices.FirstOrDefault(x => x.Properties.DeviceType == type);

			if (foundDevice != null)
				physicalDevice = foundDevice;
		}
		else
		{
			physicalDevice = devices.First();
		}

		Console.WriteLine($"Using physical device '{physicalDevice.Properties.DeviceName}'.");
	}

	private unsafe void InitializeDevice()
	{
		uint graphicsQueueFamilyIndex, presentationQueueFamilyIndex, computeQueueFamilyIndex, transferQueueFamilyIndex;
		var properties = physicalDevice.QueueFamilyProperties;

		{
			graphicsQueueFamilyIndex = findQueueFamilyIndex(properties, (i, x) => (x.QueueFlags & QueueFlags.Graphics) != 0 && (x.QueueFlags & QueueFlags.Compute) == 0);

			if (graphicsQueueFamilyIndex == ~0u)
				graphicsQueueFamilyIndex = findQueueFamilyIndex(properties, (i, x) => (x.QueueFlags & QueueFlags.Graphics) != 0);
		}

		{
			presentationQueueFamilyIndex = findQueueFamilyIndex(properties, (i, x) => instance.Surface!.IsSupported(physicalDevice, (uint)i));
		}

		{
			computeQueueFamilyIndex = findQueueFamilyIndex(properties, (i, x) => (x.QueueFlags & QueueFlags.Compute) != 0 && (x.QueueFlags & QueueFlags.Graphics) == 0);

			if (computeQueueFamilyIndex == ~0u)
				computeQueueFamilyIndex = findQueueFamilyIndex(properties, (i, x) => (x.QueueFlags & QueueFlags.Compute) != 0);
		}

		{
			transferQueueFamilyIndex = findQueueFamilyIndex(properties, (i, x) => (x.QueueFlags & QueueFlags.Transfer) != 0 && (x.QueueFlags & QueueFlags.Graphics) == 0 && (x.QueueFlags & QueueFlags.Compute) == 0);

			if (transferQueueFamilyIndex == ~0u)
				transferQueueFamilyIndex = findQueueFamilyIndex(properties, (i, x) => (x.QueueFlags & QueueFlags.Transfer) != 0 && (x.QueueFlags & QueueFlags.Compute) == 0);

			if (transferQueueFamilyIndex == ~0u)
				transferQueueFamilyIndex = findQueueFamilyIndex(properties, (i, x) => (x.QueueFlags & QueueFlags.Transfer) != 0);
		}

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

		using var computeDeviceQueueCreateInfo = new DeviceQueueCreateInfo(
			next: default,
			flags: default,
			queueFamilyIndex: computeQueueFamilyIndex,
			queuePriorities: [1f]
		);

		using var transferDeviceQueueCreateInfo = new DeviceQueueCreateInfo(
			next: default,
			flags: default,
			queueFamilyIndex: transferQueueFamilyIndex,
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

		var descriptorIndexingFeatures = new PhysicalDeviceDescriptorIndexingFeatures(
			next: (nint)(&synchronization2Features),
			shaderInputAttachmentArrayDynamicIndexing: false,
			shaderUniformTexelBufferArrayDynamicIndexing: false,
			shaderStorageTexelBufferArrayDynamicIndexing: false,
			shaderUniformBufferArrayNonUniformIndexing: false,
			shaderSampledImageArrayNonUniformIndexing: false,
			shaderStorageBufferArrayNonUniformIndexing: false,
			shaderStorageImageArrayNonUniformIndexing: false,
			shaderInputAttachmentArrayNonUniformIndexing: false,
			shaderUniformTexelBufferArrayNonUniformIndexing: false,
			shaderStorageTexelBufferArrayNonUniformIndexing: false,
			descriptorBindingUniformBufferUpdateAfterBind: false,
			descriptorBindingSampledImageUpdateAfterBind: false,
			descriptorBindingStorageImageUpdateAfterBind: false,
			descriptorBindingStorageBufferUpdateAfterBind: false,
			descriptorBindingUniformTexelBufferUpdateAfterBind: false,
			descriptorBindingStorageTexelBufferUpdateAfterBind: false,
			descriptorBindingUpdateUnusedWhilePending: false,
			descriptorBindingPartiallyBound: true,
			descriptorBindingVariableDescriptorCount: false,
			runtimeDescriptorArray: false
		);

		using var deviceCreateInfo = new DeviceCreateInfo(
			next: (nint)(&descriptorIndexingFeatures),
			flags: default,
			queueCreateInfos: new[] { graphicsDeviceQueueCreateInfo, presentationDeviceQueueCreateInfo, computeDeviceQueueCreateInfo, transferDeviceQueueCreateInfo }.DistinctBy(x => x.QueueFamilyIndex).ToArray(),
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

		var map = new Dictionary<uint, QueueContext>();

		foreach (var x in stackalloc[] { graphicsQueueFamilyIndex, presentationQueueFamilyIndex, computeQueueFamilyIndex, transferQueueFamilyIndex })
			if (!map.ContainsKey(x))
				map[x] = new QueueContext(this, x);

		graphicsQueueContext = map[graphicsQueueFamilyIndex];
		presentationQueueContext = map[presentationQueueFamilyIndex];
		computeQueueContext = map[computeQueueFamilyIndex];
		transferQueueContext = map[transferQueueFamilyIndex];

		static uint findQueueFamilyIndex(QueueFamilyProperties[] properties, Func<int, QueueFamilyProperties, bool> predicate) =>
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
			.Append(-1)
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
		swapchainExtent = swapchainProperties.GetExtent(framebufferWidth, framebufferHeight);
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
			imageExtent: swapchainExtent,
			imageArrayLayers: 1,
			imageUsage: ImageUsage.ColorAttachment,
			imageSharingMode: SharingMode.Concurrent,
			queueFamilyIndices: new[] { graphicsQueueContext, presentationQueueContext, computeQueueContext, transferQueueContext }.DistinctBy(x => x.FamilyIndex).Select(x => x.FamilyIndex).ToArray(),
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
		using var bindingFlagsCreateInfo = new DescriptorSetLayoutBindingFlagsCreateInfo(
			next: default,
			bindingFlags: new DescriptorBindingFlags[] { default, DescriptorBindingFlags.PartiallyBound }.Concat(Enumerable.Repeat(DescriptorBindingFlags.PartiallyBound, MAX_TEXTURES + MAX_BUFFERS)).ToArray()
		);

		var globalUniformsBinding = new DescriptorSetLayoutBinding(
			binding: (uint)GLOBAL_UNIFORMS_BINDING,
			descriptorType: DescriptorType.UniformBuffer,
			descriptorCount: 1,
			stage: ShaderStage.AllGraphics,
			immutableSamplers: null
		);

		var objectUniformsBinding = new DescriptorSetLayoutBinding(
			binding: (uint)OBJECT_UNIFORMS_BINDING,
			descriptorType: DescriptorType.UniformBuffer,
			descriptorCount: 1,
			stage: ShaderStage.AllGraphics,
			immutableSamplers: null
		);

		var texturesBindings = Enumerable.Range(TEXTURES_BINDING, MAX_TEXTURES).Select(x => new DescriptorSetLayoutBinding(
				binding: (uint)x,
				descriptorType: DescriptorType.CombinedImageSampler,
				descriptorCount: 1,
				stage: ShaderStage.AllGraphics,
				immutableSamplers: null
			)
		);

		var buffersBindings = Enumerable.Range(BUFFERS_BINDING, MAX_BUFFERS).Select(x => new DescriptorSetLayoutBinding(
				binding: (uint)x,
				descriptorType: DescriptorType.StorageBuffer,
				descriptorCount: 1,
				stage: ShaderStage.AllGraphics,
				immutableSamplers: null
			)
		);

		using var descriptorSetLayoutCreateInfo = new DescriptorSetLayoutCreateInfo(
			next: default,
			flags: DescriptorSetLayoutCreateFlags.PushDescriptor,
			bindings: new[] { [globalUniformsBinding, objectUniformsBinding], texturesBindings, buffersBindings }.SelectMany(x => x).ToArray()
		);

		descriptorSetLayout = descriptorSetLayoutCreateInfo.CreateDescriptorSetLayout(device, allocator);
	}

	private void InitializeGlobalUniforms()
	{
		DeviceSize size = (ulong)Marshal.SizeOf<GlobalUniforms>();

		globalUniformsBuffers = new VkBuffer[maxFrames];
		globalUniformsMemories = new DeviceMemory[maxFrames];
		globalUniformsLocations = new nint[maxFrames];

		for (int i = 0; i < maxFrames; i++)
		{
			CreateBuffer(size, BufferUsage.UniformBuffer, out VkBuffer buffer);
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
			setLayouts: [descriptorSetLayout],
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
			extent: new(swapchainExtent.Width, swapchainExtent.Height, 1),
			mipLevels: 1,
			arrayLayers: 1,
			samples: SampleCount.Bit1,
			tiling: ImageTiling.Optimal,
			usage: ImageUsage.DepthStencilAttachment | ImageUsage.Sampled,
			sharingMode: SharingMode.Exclusive,
			queueFamilyIndices: null,
			initialLayout: ImageLayout.Undefined
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

	private void InitializeSyncObjects()
	{
		var fenceCreateInfo = new FenceCreateInfo(
			next: default,
			flags: default
		);

		imageAvailableFence = new Fence[maxFrames];
		inFlightTimelineValues = new ulong[maxFrames];

		for (int i = 0; i < maxFrames; i++)
		{
			imageAvailableFence[i] = fenceCreateInfo.CreateFence(device, allocator);
			inFlightTimelineValues[i] = 0;
		}
	}

	public void DeviceWaitIdle() => device.WaitIdle();

	public void Resize()
	{
		DeviceWaitIdle();

		foreach (var x in swapchainImageViews)
			x.Dispose();

		swapchain.Dispose();
		depthImageView.Dispose();
		depthImageMemory.Dispose();
		depthImage.Dispose();

		InitializeSwapchain();
		InitializeImageViews();
		InitializeDepthImage();
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
		InitializeSyncObjects();

		toBeDisposed = new List<IDisposable>[maxFrames];
		for (int i = 0; i < maxFrames; i++)
			toBeDisposed[i] = new();

		Console.WriteLine("Vulkan Initialized!");
	}

	public void Dispose()
	{
		foreach (var x in new[] { graphicsQueueContext, presentationQueueContext, computeQueueContext, transferQueueContext }.DistinctBy(x => x.FamilyIndex))
			x.Dispose();

		foreach (var x in Assets)
			(x.Target as IDisposable)?.Dispose();

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		foreach (var list in toBeDisposed)
		{
			foreach (var x in list)
				x.Dispose();

			list.Clear();
		}

		depthImageView.Dispose();
		depthImage.Dispose();
		depthImageMemory.Dispose();

		foreach (var x in globalUniformsMemories)
			x.Unmap();

		foreach (var x in imageAvailableFence)
			x.Dispose();

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

		descriptorSetLayout.Dispose();

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

		Renderer.Current = this;
	}
#pragma warning restore
}

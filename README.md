# Configuration
## Gpu Selection
The renderer checks for the following environment variables, in this order:
- `VK_PHYSICAL_DEVICE_DEVICE_ID` - Gpu device id. (value must be a base 10 integer)
- `VK_PHYSICAL_DEVICE_VENDOR_ID` - Gpu vendor id. (value must be a base 10 integer)
- `VK_PHYSICAL_DEVICE_TYPE` - Gpu type. (see [Vulkan.PhysicalDeviceType](Vulkan/Vulkan/PhysicalDeviceType.cs))

# Scene
It uses a **left handed, +y up** coordinate system. There is one root `SceneObject` and from there everything grows as a tree. Every `SceneObject` has a `Layer` property, which is used to determine by which camera the object will be rendered - it is a mask. For an object to be rendered by a camera the expression `(camera.Layer & object.Layer) != 0` must be true. An object can receive events when the scene started/first loaded, before every frame and when a collision happens.
## UI
The canvas is the root `SceneObject` for ui elements. If the scene root is the main tree of objects, then the canvas is a sub-tree which contains ui elements/objects. A ui element is not needed to be inside a canvas, it just won't receive ui events. UI elements are rendered twice into 2 different textures - the first is the normal color texture that gets displayed to the user and the second one is used for mapping a mouse click to a ui element (sample the texture at these coordinates). Rendering to textures happens the same way as in a scene - with the same layer. The (first) color texture is rendered on top of everything to the window directly. UI elements are rendered onto the textures using orthographic projection so although they look 2D there is still a *z* component.

# Shaders
## Compilation
By default it uses **hlsl** as the shader language but users can also write their shaders in **glsl** too. The shader compiler reads all lines that start with `#pragma` for more options. They are not case sensitive:
### Pipeline Specific Options:
- stage {[Vulkan.ShaderStage](Vulkan/Vulkan/ShaderStage.cs)} [entryPoint]
- cull {[Vulkan.CullMode](Vulkan/Vulkan/CullMode.cs)}
- frontface {[Vulkan.FrontFace](Vulkan/Vulkan/FrontFace.cs)}
- blend {*disabled*|*off*}|{{[Vulkan.BlendFactor](Vulkan/Vulkan/BlendFactor.cs)} [[Vulkan.BlendOp](Vulkan/Vulkan/BlendOp.cs)] {[Vulkan.BlendFactor](Vulkan/Vulkan/BlendFactor.cs)}}
### Compiler Specific Options:
- language {*glsl*|*hlsl*}
- {[Vulkan.ShaderCompiler.Limit](Vulkan/Vulkan/ShaderCompiler/Limit.cs)} {value}
- environment {[Vulkan.ShaderCompiler.TargetEnvironment](Vulkan/Vulkan/ShaderCompiler/TargetEnvironment.cs)} {[Vulkan.ShaderCompiler.EnvironmentVersion](Vulkan/Vulkan/ShaderCompiler/EnvironmentVersion.cs)}
- spirv {[Vulkan.ShaderCompiler.SPIRVVersion](Vulkan/Vulkan/ShaderCompiler/SPIRVVersion.cs)}
- optimization {*disabled*|*off*}|{[Vulkan.ShaderCompiler.OptimizationLevel](Vulkan/Vulkan/ShaderCompiler/OptimizationLevel.cs)}
- GenerateDebugInfo
- WarningsAsErrors
- SuppressWarnings
- AutoBindUniforms
- AutoCombinedImageSampler
- HLSLIOMapping
- HLSLOffsets
- PreserveBindings
- AutoMapLocations
- HLSLFunctionality1
- HLSL16BitTypes
- VulkanRulesRelaxed
- InvertY
- NanClamp
## Depth
The renderer only uses a depth buffer (not stencil), and the value is reversed (reversed-depth). Objects closer to the camera have a greater depth value.
## Uniforms
Properteis that do not change for every object are passed as a uniform buffer at binding 0. Properties (not defined by user) that change for every object are passed as push constants. Everything else (properties set from a material) gets packed into a uniform buffer at binding 1. The type of a material property must be a non-generic value type (otherwise it is ignored), and they all go into the uniform buffer.
## Textures
Textures are passed only for sampling (readonly) as a texture/sampler pair. Each shader stage has a default maximum of 16 texture/sampler pairs. Every pair gets their own binding, starting from 2. This maximum value can be changed by the `VK_MAX_TEXTURES` environment variable.

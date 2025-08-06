#pragma warning disable CS8618

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Vulkan;

namespace Renderer;

public sealed class Texture : IDisposable
{
	internal readonly Vulkan.Image Image;
	internal readonly Vulkan.ImageView ImageView;
	internal readonly Vulkan.DeviceMemory Memory;
	internal readonly Vulkan.Sampler Sampler;

	public int Width { get; }
	public int Height { get; }
	public uint[] Colors { get; } // msb A8R8G8B8 lsb

	public void Dispose() 
	{
		Sampler.Dispose();
		ImageView.Dispose();
		Image.Dispose();
		Memory.Dispose();
	}

	public Texture(Vulkan.Program vk, string filename) 
	{
		var extension = Path.GetExtension(filename).ToLower();
		switch (extension) 
		{
			case ".png":
				var png = PNG.FromFile(filename);
				this.Width = png.Width;
				this.Height = png.Height;
				this.Colors = png.Colors;
				break;
			default:
				throw new InvalidOperationException($"Failed to parse texture of type '{extension}'.");
		}

		vk.CreateTexture(
			ref Unsafe.As<uint, byte>(ref MemoryMarshal.GetArrayDataReference(Colors)),
			Width,
			Height,
			ImageType.Generic2D,
			Format.B8G8R8A8UNorm,
			out Image,
			out ImageView,
			out Memory,
			out Sampler
		);
	}
}

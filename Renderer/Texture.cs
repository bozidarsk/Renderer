#pragma warning disable CS8618

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Vulkan;

namespace Renderer;

public abstract class Texture : IDisposable
{
	public Vulkan.Image Image;
	public Vulkan.ImageView ImageView;
	public Vulkan.DeviceMemory Memory;
	public Vulkan.Sampler Sampler;

	public int Width { protected set; get; }
	public int Height { protected set; get; }
	public bool IsTransparent { protected set; get; }
	public uint[] Colors { protected set; get; } // msb A8R8G8B8 lsb

	public static Texture FromFile(string filename) 
	{
		if (filename == null)
			throw new ArgumentNullException();

		if (!File.Exists(filename))
			throw new FileNotFoundException();

		string extension = Path.GetExtension(filename);

		return extension switch 
		{
			".png" => PNG.FromFile(filename),
			_ => throw new InvalidOperationException($"Failed to parse texture of type '{extension}'.")
		};
	}

	public abstract void Save(string filename);

	public void Initialize(Vulkan.Program vk) 
	{
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

	public void Dispose() 
	{
		Sampler.Dispose();
		ImageView.Dispose();
		Image.Dispose();
		Memory.Dispose();
	}
}

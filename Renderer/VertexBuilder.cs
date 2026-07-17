using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Renderer;

public class VertexBuilder
{
	private static readonly AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("VertexTypesAssembly"), AssemblyBuilderAccess.Run);
	private static readonly ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("VertexTypesAssembly");
	private static ulong typeId = 0;

	private readonly TypeBuilder typeBuilder = moduleBuilder.DefineType($"Vertex_{++typeId}", TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed);

	public VertexBuilder AddField<T>(string name) => AddField(typeof(T), name);
	public VertexBuilder AddField(Type type, string name)
	{
		if (!type.IsValueType || type.IsGenericType)
			throw new ArgumentException("Type must be a non-generic value type.");

		typeBuilder.DefineField(name, type, FieldAttributes.Public);

		return this;
	}

	public Type CreateType() => typeBuilder.CreateType();
}

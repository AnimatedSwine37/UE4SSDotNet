﻿using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UE4SSDotNetFramework.Framework;

namespace UE4SSDotNetFramework.Framework;

internal static class Shared
{
	internal const int checksum = 0x2F0;
	internal static Dictionary<int, IntPtr> userFunctions = new();
	private const string dynamicTypesAssemblyName = "UnrealEngine.DynamicTypes";

	private static readonly ModuleBuilder moduleBuilder = AssemblyBuilder
		.DefineDynamicAssembly(new(dynamicTypesAssemblyName), AssemblyBuilderAccess.RunAndCollect)
		.DefineDynamicModule(dynamicTypesAssemblyName);

	private static readonly Type[] delegateCtorSignature = { typeof(object), typeof(IntPtr) };
	private static Dictionary<string, Delegate> delegatesCache = new();
	private static Dictionary<string, Type> delegateTypesCache = new();

	private const MethodAttributes ctorAttributes =
		MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public;

	private const MethodImplAttributes implAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed;

	private const MethodAttributes invokeAttributes = MethodAttributes.Public | MethodAttributes.HideBySig |
	                                                  MethodAttributes.NewSlot | MethodAttributes.Virtual;

	private const TypeAttributes delegateTypeAttributes = TypeAttributes.Class | TypeAttributes.Public |
	                                                      TypeAttributes.Sealed | TypeAttributes.AnsiClass |
	                                                      TypeAttributes.AutoClass;

	internal static unsafe Dictionary<int, IntPtr> Load(IntPtr* events, Assembly pluginAssembly)
	{
		unchecked {
			Type[] types = pluginAssembly.GetTypes();

			foreach (Type type in types) {
				MethodInfo[] methods = type.GetMethods();

				if (type.Name == "Main" && type.IsPublic) {
					foreach (MethodInfo method in methods) {
						if (method.IsPublic && method.IsStatic && !method.IsGenericMethod) {
							ParameterInfo[] parameterInfos = method.GetParameters();

							if (parameterInfos.Length <= 1) {
								if (method.Name == "StartMod") {
									if (parameterInfos.Length == 0)
										events[0] = GetFunctionPointer(method);
									else
										throw new ArgumentException(method.Name + " should not have arguments");

									continue;
								}

								if (method.Name == "StopMod") {
									if (parameterInfos.Length == 0)
										events[1] = GetFunctionPointer(method);
									else
										throw new ArgumentException(method.Name + " should not have arguments");

									continue;
								}
								
								if (method.Name == "ProgramStart") {
									if (parameterInfos.Length == 0)
										events[2] = GetFunctionPointer(method);
									else
										throw new ArgumentException(method.Name + " should not have arguments");

									continue;
								}
								
								if (method.Name == "UnrealInit") {
									if (parameterInfos.Length == 0)
										events[3] = GetFunctionPointer(method);
									else
										throw new ArgumentException(method.Name + " should not have arguments");

									continue;
								}
								
								if (method.Name == "Update") {
									if (parameterInfos.Length == 0)
										events[4] = GetFunctionPointer(method);
									else
										throw new ArgumentException(method.Name + " should not have arguments");
								}
							}
						}
					}
				}

				foreach (MethodInfo method in methods) {
					if (method.IsPublic && method.IsStatic && !method.IsGenericMethod) {
						ParameterInfo[] parameterInfos = method.GetParameters();

						if (parameterInfos.Length <= 1) {
							if (parameterInfos.Length == 1 && parameterInfos[0].ParameterType != typeof(ObjectReference))
								continue;

							string name = type.FullName + "." + method.Name;

							userFunctions.Add(name.GetHashCode(StringComparison.Ordinal), GetFunctionPointer(method));
						}
					}
				}
			}
		}

		GC.Collect();
		GC.WaitForPendingFinalizers();

		return userFunctions;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string GetTypeName(Type type) => type.FullName.Replace(".", string.Empty, StringComparison.Ordinal);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string GetMethodName(Type[] parameters, Type returnType) {
		string name = GetTypeName(returnType);

		foreach (Type type in parameters) {
			name += '_' + GetTypeName(type);
		}

		return name;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Type GetDelegateType(Type[] parameters, Type returnType) {
		string methodName = GetMethodName(parameters, returnType);

		return delegateTypesCache.GetOrAdd(methodName, () => MakeDelegate(parameters, returnType, methodName));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Type MakeDelegate(Type[] types, Type returnType, string name) {
		TypeBuilder builder = moduleBuilder.DefineType(name, delegateTypeAttributes, typeof(MulticastDelegate));

		builder.DefineConstructor(ctorAttributes, CallingConventions.Standard, delegateCtorSignature).SetImplementationFlags(implAttributes);
		builder.DefineMethod("Invoke", invokeAttributes, returnType, types).SetImplementationFlags(implAttributes);

		return builder.CreateTypeInfo();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static IntPtr GetFunctionPointer(MethodInfo method) {
		string methodName = $"{ method.DeclaringType.FullName }.{ method.Name }";

		Delegate dynamicDelegate = delegatesCache.GetOrAdd(methodName, () => {
			ParameterInfo[] parameterInfos = method.GetParameters();
			Type[] parameterTypes = new Type[parameterInfos.Length];

			for (int i = 0; i < parameterTypes.Length; i++) {
				parameterTypes[i] = parameterInfos[i].ParameterType;
			}

			return method.CreateDelegate(GetDelegateType(parameterTypes, method.ReturnType));
		});

		return Collector.GetFunctionPointer(dynamicDelegate);
	}
}
internal enum PropertyType
{
	ObjectProperty,
	ClassProperty,
	Int8Property,
	Int16Property,
	IntProperty,
	Int64Property,
	ByteProperty,
	UInt16Property,
	UInt32Property,
	UInt64Property,
	StructProperty,
	ArrayProperty,
	FloatProperty,
	DoubleProperty,
	BoolProperty,
	EnumProperty,
	WeakObjectProperty,
	NameProperty,
	TextProperty,
	StrProperty,
	SoftClassProperty,
	InterfaceProperty,
	Invalid,
};

[StructLayout(LayoutKind.Sequential)]
partial struct UnArray
{
	private IntPtr Data;
	private ulong Length;
	private PropertyType Type;
}

[StructLayout(LayoutKind.Sequential)]
partial struct WeakObjectPtr
{
	private int ObjectIndex;
	private int ObjectSerialNumber;
}

static unsafe partial class Debug {
	[DllImport("UE4SS.dll", EntryPoint = "?Log@Debug@Framework@DotNetLibrary@RC@@SAXW4LogLevel@54@PEBD@Z")]
	internal static extern void Log(LogLevel level, byte[] message);
}

internal static unsafe class Object
{
	[DllImport("UE4SS.dll", EntryPoint = "?IsValid@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@@Z")]
	internal static extern bool IsValid(IntPtr @object);
	[DllImport("UE4SS.dll", EntryPoint = "?Invoke@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBD@Z")]
	internal static extern bool Invoke(IntPtr @object, byte[] command);
	[DllImport("UE4SS.dll", EntryPoint = "?Find@Object@Framework@DotNetLibrary@RC@@SAPEAVUObject@Unreal@4@PEBD@Z")]
	internal static extern IntPtr Find(byte[] name);
	[DllImport("UE4SS.dll", EntryPoint = "?GetName@Object@Framework@DotNetLibrary@RC@@SAXPEAVUObject@Unreal@4@PEAD@Z")]
	internal static extern void GetName(IntPtr @object, byte[] name);
	[DllImport("UE4SS.dll", EntryPoint = "?GetFullName@Object@Framework@DotNetLibrary@RC@@SAXPEAVUObject@Unreal@4@PEAD@Z")]
	internal static extern void GetFullName(IntPtr @object, byte[] name);
	[DllImport("UE4SS.dll", EntryPoint = "?GetClass@Object@Framework@DotNetLibrary@RC@@SAXPEAVUObject@Unreal@4@PEAPEAVUClass@64@@Z")]
	internal static extern void GetClass(IntPtr @object, ref IntPtr @class);
	[DllImport("UE4SS.dll", EntryPoint = "?GetOuter@Object@Framework@DotNetLibrary@RC@@SAXPEAVUObject@Unreal@4@PEAPEAV564@@Z")]
	internal static extern void GetOuter(IntPtr @object, ref IntPtr outer);
	[DllImport("UE4SS.dll", EntryPoint = "?IsAnyClass@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@@Z")]
	internal static extern void IsAnyClass(IntPtr @object);
	[DllImport("UE4SS.dll", EntryPoint = "?IsAnyClass@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@@Z")]
	internal static extern void GetWorld(IntPtr @object, ref IntPtr world);
	[DllImport("UE4SS.dll", EntryPoint = "?IsA@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEAVUClass@64@@Z")]
	internal static extern void IsA(IntPtr @object, IntPtr @class);
	[DllImport("UE4SS.dll", EntryPoint = "?HasAllFlags@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@W4EObjectFlags@64@@Z")]
	internal static extern void HasAllFlags(IntPtr @object, int flags);
	[DllImport("UE4SS.dll", EntryPoint = "?HasAnyFlags@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@W4EObjectFlags@64@@Z")]
	internal static extern void HasAnyFlags(IntPtr @object, int flags);
	[DllImport("UE4SS.dll", EntryPoint = "?HasAnyInternalFlags@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@W4EInternalObjectFlags@64@@Z")]
	internal static extern void HasAnyInternalFlags(IntPtr @object, int flags);
	[DllImport("UE4SS.dll", EntryPoint = "?GetObjectReference@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAPEAV564@@Z")]
	internal static extern bool GetObjectReference(IntPtr @object, byte[] name, ref IntPtr value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetBool@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEA_N@Z")]
	internal static extern bool GetBool(IntPtr @object, byte[] name, ref bool value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetByte@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAE@Z")]
	internal static extern bool GetByte(IntPtr @object, byte[] name, ref byte value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetShort@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAF@Z")]
	internal static extern bool GetShort(IntPtr @object, byte[] name, ref short value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetInt@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAH@Z")]
	internal static extern bool GetInt(IntPtr @object, byte[] name, ref int value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetLong@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEA_J@Z")]
	internal static extern bool GetLong(IntPtr @object, byte[] name, ref long value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetUShort@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAG@Z")]
	internal static extern bool GetUShort(IntPtr @object, byte[] name, ref ushort value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetUInt@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAI@Z")]
	internal static extern bool GetUInt(IntPtr @object, byte[] name, ref uint value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetULong@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEA_K@Z")]
	internal static extern bool GetULong(IntPtr @object, byte[] name, ref ulong value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetStruct@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAPEAVUStruct@64@@Z")]
	internal static extern bool GetStruct(IntPtr @object, byte[] name, ref IntPtr value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetArray@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAUArray@234@@Z")]
	internal static extern bool GetArray(IntPtr @object, byte[] name, ref UnArray value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetFloat@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAM@Z")]
	internal static extern bool GetFloat(IntPtr @object, byte[] name, ref float value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetDouble@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAN@Z")]
	internal static extern bool GetDouble(IntPtr @object, byte[] name, ref double value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetEnum@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAH@Z")]
	internal static extern bool GetEnum(IntPtr @object, byte[] name, ref int value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetWeakObject@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAUFWeakObjectPtr@64@@Z")]
	internal static extern bool GetWeakObject(IntPtr @object, byte[] name, ref WeakObjectPtr value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetString@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAD@Z")]
	internal static extern bool GetString(IntPtr @object, byte[] name, byte[] value);
	[DllImport("UE4SS.dll", EntryPoint = "?GetText@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAD@Z")]
	internal static extern bool GetText(IntPtr @object, byte[] name, byte[] value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetObjectReference@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBD0@Z")]
	internal static extern bool SetObjectReference(IntPtr @object, byte[] name, IntPtr value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetByte@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDE@Z")]
	internal static extern bool SetBool(IntPtr @object, byte[] name, bool value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetByte@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDE@Z")]
	internal static extern bool SetByte(IntPtr @object, byte[] name, byte value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetShort@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDF@Z")]
	internal static extern bool SetShort(IntPtr @object, byte[] name, short value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetInt@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDH@Z")]
	internal static extern bool SetInt(IntPtr @object, byte[] name, int value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetLong@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBD_J@Z")]
	internal static extern bool SetLong(IntPtr @object, byte[] name, long value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetUShort@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDG@Z")]
	internal static extern bool SetUShort(IntPtr @object, byte[] name, ushort value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetUInt@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDI@Z")]
	internal static extern bool SetUInt(IntPtr @object, byte[] name, uint value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetULong@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBD_K@Z")]
	internal static extern bool SetULong(IntPtr @object, byte[] name, ulong value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetStruct@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAVUStruct@64@@Z")]
	internal static extern bool SetStruct(IntPtr @object, byte[] name, IntPtr value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetArray@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDUUnArray@234@@Z")]
	internal static extern bool SetArray(IntPtr @object, byte[] name, UnArray value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetFloat@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDM@Z")]
	internal static extern bool SetFloat(IntPtr @object, byte[] name, float value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetDouble@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDN@Z")]
	internal static extern bool SetDouble(IntPtr @object, byte[] name, double value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetEnum@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDH@Z")]
	internal static extern bool SetEnum(IntPtr @object, byte[] name, int value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetString@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBD1@Z")]
	internal static extern bool SetString(IntPtr @object, byte[] name, byte[] value);
	[DllImport("UE4SS.dll", EntryPoint = "?SetText@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBD1@Z")]
	internal static extern bool SetText(IntPtr @object, byte[] name, byte[] value);
}

internal static unsafe class Struct
{
	[DllImport("UE4SS.dll", EntryPoint = "?GetSuperStruct@Struct@Framework@DotNetLibrary@RC@@CAPEAVUClass@Unreal@4@PEAVUStruct@64@@Z")]
	internal static extern IntPtr GetSuperStruct(IntPtr @struct);
	[DllImport("UE4SS.dll", EntryPoint = "?ForEachFunction@Struct@Framework@DotNetLibrary@RC@@CAXPEAVUStruct@Unreal@4@P6AXPEAVUFunction@64@@Z@Z")]
	internal static extern bool ForEachFunction(IntPtr @struct, delegate* unmanaged[Cdecl]<IntPtr, void> callback);
	[DllImport("UE4SS.dll", EntryPoint = "?ForEachProperty@Struct@Framework@DotNetLibrary@RC@@CAXPEAVUStruct@Unreal@4@P6AXPEAVFProperty@64@@Z@Z")]
	internal static extern bool ForEachProperty(IntPtr @struct, delegate* unmanaged[Cdecl]<IntPtr, void> callback);
}

internal static unsafe class Class
{
	[DllImport("UE4SS.dll", EntryPoint = "?GetCDO@Class@Framework@DotNetLibrary@RC@@CAXPEAVUClass@Unreal@4@PEAPEAVUObject@64@@Z")]
	internal static extern void GetCDO(IntPtr @class, ref IntPtr cdo);
	[DllImport("UE4SS.dll", EntryPoint = "?IsChildOf@Class@Framework@DotNetLibrary@RC@@CA_NPEAVUClass@Unreal@4@0@Z")]
	internal static extern bool IsChildOf(IntPtr @class, IntPtr parent);
}
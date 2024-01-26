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

static unsafe partial class Debug {
	[DllImport("UE4SS.dll", EntryPoint = @"?Log@Debug@Framework@DotNetLibrary@RC@@SAXW4LogLevel@54@PEBD@Z")]
	internal static extern void Log(LogLevel level, byte[] message);
}

internal static unsafe class Object
{
	[DllImport("UE4SS.dll", EntryPoint = @"?IsValid@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@@Z")]
	internal static extern bool IsValid(IntPtr @object);
	[DllImport("UE4SS.dll", EntryPoint = @"?Invoke@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBD@Z")]
	internal static extern bool Invoke(IntPtr @object, byte[] command);
	[DllImport("UE4SS.dll", EntryPoint = @"?Find@Object@Framework@DotNetLibrary@RC@@SAPEAVUObject@Unreal@4@PEBD@Z")]
	internal static extern IntPtr Find(byte[] name);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetName@Object@Framework@DotNetLibrary@RC@@SAXPEAVUObject@Unreal@4@PEAD@Z")]
	internal static extern void GetName(IntPtr @object, byte[] name);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetBool@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEA_N@Z")]
	internal static extern bool GetBool(IntPtr @object, byte[] name, ref bool value);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetByte@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAE@Z")]
	internal static extern bool GetByte(IntPtr @object, byte[] name, ref byte value);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetShort@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAF@Z")]
	internal static extern bool GetShort(IntPtr @object, byte[] name, ref short value);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetInt@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAH@Z")]
	internal static extern bool GetInt(IntPtr @object, byte[] name, ref int value);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetLong@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEA_J@Z")]
	internal static extern bool GetLong(IntPtr @object, byte[] name, ref long value);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetUShort@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAG@Z")]
	internal static extern bool GetUShort(IntPtr @object, byte[] name, ref ushort value);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetUInt@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAI@Z")]
	internal static extern bool GetUInt(IntPtr @object, byte[] name, ref uint value);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetULong@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEA_K@Z")]
	internal static extern bool GetULong(IntPtr @object, byte[] name, ref ulong value);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetFloat@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAM@Z")]
	internal static extern bool GetFloat(IntPtr @object, byte[] name, ref float value);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetDouble@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAN@Z")]
	internal static extern bool GetDouble(IntPtr @object, byte[] name, ref double value);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetEnum@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAH@Z")]
	internal static extern bool GetEnum(IntPtr @object, byte[] name, ref int value);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetString@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAD@Z")]
	internal static extern bool GetString(IntPtr @object, byte[] name, byte[] value);
	[DllImport("UE4SS.dll", EntryPoint = @"?GetText@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDPEAD@Z")]
	internal static extern bool GetText(IntPtr @object, byte[] name, byte[] value);
	[DllImport("UE4SS.dll", EntryPoint = @"?SetBool@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBD_N@Z")]
	internal static extern bool SetBool(IntPtr @object, byte[] name, bool value);
	[DllImport("UE4SS.dll", EntryPoint = @"?SetByte@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDE@Z")]
	internal static extern bool SetByte(IntPtr @object, byte[] name, byte value);
	[DllImport("UE4SS.dll", EntryPoint = @"?SetShort@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDF@Z")]
	internal static extern bool SetShort(IntPtr @object, byte[] name, short value);
	[DllImport("UE4SS.dll", EntryPoint = @"?SetInt@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDH@Z")]
	internal static extern bool SetInt(IntPtr @object, byte[] name, int value);
	[DllImport("UE4SS.dll", EntryPoint = @"?SetLong@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBD_J@Z")]
	internal static extern bool SetLong(IntPtr @object, byte[] name, long value);
	[DllImport("UE4SS.dll", EntryPoint = @"?SetUShort@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDG@Z")]
	internal static extern bool SetUShort(IntPtr @object, byte[] name, ushort value);
	[DllImport("UE4SS.dll", EntryPoint = @"?SetUInt@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDI@Z")]
	internal static extern bool SetUInt(IntPtr @object, byte[] name, uint value);
	[DllImport("UE4SS.dll", EntryPoint = @"?SetULong@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBD_K@Z")]
	internal static extern bool SetULong(IntPtr @object, byte[] name, ulong value);
	[DllImport("UE4SS.dll", EntryPoint = @"?SetFloat@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDM@Z")]
	internal static extern bool SetFloat(IntPtr @object, byte[] name, float value);
	[DllImport("UE4SS.dll", EntryPoint = @"?SetDouble@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDN@Z")]
	internal static extern bool SetDouble(IntPtr @object, byte[] name, double value);
	[DllImport("UE4SS.dll", EntryPoint = @"?SetEnum@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBDH@Z")]
	internal static extern bool SetEnum(IntPtr @object, byte[] name, int value);
	[DllImport("UE4SS.dll", EntryPoint = @"?SetString@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBD1@Z")]
	internal static extern bool SetString(IntPtr @object, byte[] name, byte[] value);
	[DllImport("UE4SS.dll", EntryPoint = @"?SetText@Object@Framework@DotNetLibrary@RC@@SA_NPEAVUObject@Unreal@4@PEBD1@Z")]
	internal static extern bool SetText(IntPtr @object, byte[] name, byte[] value);
}
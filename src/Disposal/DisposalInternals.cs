using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Disposal {
	internal static class DisposalInternals {
		internal delegate void StructDispose<T>(ref T x) where T : struct;

		internal delegate void ClassDispose<in T>(T x) where T : class;

		private delegate Object InterlockedExchange(ref Object location, Object value);

		private static readonly MethodInfo InterlockedExchangeMethodInfo = new InterlockedExchange(Interlocked.Exchange<Object>).GetMethodInfo().GetGenericMethodDefinition();
		private static readonly MethodInfo DisposeMethodInfo = typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose));

		internal static class StructDisposerCache<T> where T : struct {
			internal static void Dispose(ref T @this) => Func.Invoke(ref @this);

			private static readonly StructDispose<T> Func = GenerateIL<StructDispose<T>>(typeof(T));
		}

		internal static readonly ConcurrentDictionary<Type, ClassDispose<Object>> DynamicCache = new ConcurrentDictionary<Type, ClassDispose<Object>>();

		internal static class ClassDisposerCache<T> where T : class {
			internal static void Dispose(T @this) {
				var type = @this.GetType();
				var func = type == typeof(T) ? Func : DynamicCache.GetOrAdd(type, ClassDisposerCache<Object>.GetClassDisposeFunc);
				func(@this);
			}

			private static readonly ClassDispose<T> Func = GetClassDisposeFunc(typeof(T));

			private static ClassDispose<T> GetClassDisposeFunc(Type type) => GenerateIL<ClassDispose<T>>(type);
		}

#if DEBUG && DYNAMIC_ASSEMBLY_SAVEABLE
		private static readonly DynamicAssembly dynamicAssembly = new DynamicAssembly();

		private class DynamicAssembly : IDisposable {
			private readonly AssemblyName assemblyName;
			private readonly AssemblyBuilder assemblyBuilder;

			public TypeBuilder TypeBuilder { get; }

			public DynamicAssembly() {
				assemblyName = new AssemblyName("Debug.DisposeMethods");
				assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave, Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace(@"file:\", String.Empty));
				var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");
				TypeBuilder = moduleBuilder.DefineType("DisposeMethods", TypeAttributes.Public);
			}

			~DynamicAssembly() {
				Dispose();
			}

			public void Dispose() {
				var type = TypeBuilder.CreateType();
				assemblyBuilder.Save(assemblyName.Name + ".dll");
			}
		}
#endif

		private static TDelegate GenerateIL<TDelegate>(Type type, [CallerMemberName] String callerMemberName = null) {
			var invokeMethodInfo = typeof(TDelegate).GetMethod(nameof(Action.Invoke));
			var returnType = invokeMethodInfo.ReturnType;
			var parameterTypes = invokeMethodInfo.GetParameters().Select(x => x.ParameterType).ToArray();
			var dynamicMethod = new DynamicMethod($"{callerMemberName}_{type.Name}", returnType, parameterTypes, restrictedSkipVisibility: true);
			var ilGenerator = dynamicMethod.GetILGenerator();

			var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			var disposables = fields.Where(x => typeof(IDisposable).IsAssignableFrom(x.FieldType));
			EmitIL(ilGenerator, disposables);

#if DEBUG && DYNAMIC_ASSEMBLY_SAVEABLE
			var methodBuilder = dynamicAssembly.TypeBuilder.DefineMethod($"{callerMemberName}_{type.Name}", MethodAttributes.Public | MethodAttributes.Static, returnType, parameterTypes);

			ilGenerator = methodBuilder.GetILGenerator();
			EmitIL(ilGenerator, disposables);
#endif

			return (TDelegate) (Object) dynamicMethod.CreateDelegate(typeof(TDelegate));
		}

		private static void EmitIL(ILGenerator ilGenerator, IEnumerable<FieldInfo> disposables) {
			foreach (var disposable in disposables) {
				ilGenerator.Emit(OpCodes.Ldarg_0);
				ilGenerator.Emit(OpCodes.Ldflda, disposable);
				if (disposable.FieldType.GetTypeInfo().IsValueType)
					ilGenerator.Emit(OpCodes.Call, disposable.FieldType.GetMethod(nameof(IDisposable.Dispose)));
				else {
					ilGenerator.Emit(OpCodes.Ldnull);
					ilGenerator.Emit(OpCodes.Call, InterlockedExchangeMethodInfo.MakeGenericMethod(disposable.FieldType));
					ilGenerator.Emit(OpCodes.Dup);

					var dispose = ilGenerator.DefineLabel();
					ilGenerator.Emit(OpCodes.Brtrue, dispose);

					ilGenerator.Emit(OpCodes.Pop);
					var doNotDispose = ilGenerator.DefineLabel();
					ilGenerator.Emit(OpCodes.Br, doNotDispose);

					ilGenerator.MarkLabel(dispose);
					ilGenerator.Emit(OpCodes.Callvirt, DisposeMethodInfo);

					ilGenerator.MarkLabel(doNotDispose);
				}
			}
			ilGenerator.Emit(OpCodes.Ret);
		}
	}
}
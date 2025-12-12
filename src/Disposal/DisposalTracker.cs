using System.Collections.Concurrent;
using System.Reflection;
using static System.Reflection.BindingFlags;

namespace Disposal;

public sealed class DisposalTracker(Object target) : IAsyncDisposable
{
	private readonly Object target = target ?? throw new ArgumentNullException(nameof(target));

	private volatile Status status = Status.Alive;
	private InterlockedUInt32 useCount;
	private readonly TaskCompletionSource waitingToDisposeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

	public async ValueTask DisposeAsync()
	{
		status = Status.Disposing;
		if (useCount.Read() != 0)
			await waitingToDisposeTcs.Task;
		try
		{
			await DisposeTargetObjectFields();
		}
		finally
		{
			status = Status.Disposed;
		}
	}

	internal GuardReleaser GetGuard()
	{
		if (status != Status.Alive)
			throw new ObjectDisposedException(target.GetType().FullName);
		useCount.Increment();
		return new GuardReleaser(this);
	}

	internal readonly struct GuardReleaser(DisposalTracker disposalTracker) : IDisposable
	{
		public void Dispose() => Release();

		void Release()
		{
			if (disposalTracker.useCount.Decrement() == 0 && disposalTracker.status != Status.Alive)
				disposalTracker.waitingToDisposeTcs.SetResult();
		}
	}

	private async ValueTask DisposeTargetObjectFields()
	{
		var fieldGetters = ReflectionCache.GetFieldGetters(target);
		foreach (var getField in fieldGetters)
		{
			switch (getField(target))
			{
				case DisposalTracker:
					break;
				case IAsyncDisposable asyncDisposableField:
					await asyncDisposableField.DisposeAsync();
					break;
				case IDisposable disposableField:
					disposableField.Dispose();
					break;
			}
		}
	}

	private static class ReflectionCache
	{
		private static readonly ConcurrentDictionary<Type, List<Func<Object, Object?>>> reflectionCache = new();

		public static List<Func<Object, Object?>> GetFieldGetters(Object target)
		{
			return reflectionCache.GetOrAdd(target.GetType(), ValueFactory);

			static List<Func<Object, Object?>> ValueFactory(Type t) =>
				t.GetFields(Instance | Public | NonPublic)
					.Where(fi => fi.GetCustomAttribute<DisposalIgnoreAttribute>() == null && fi.FieldType != typeof(DisposalTracker))
					.Select(fi => fi.CreateFieldGetter())
					.ToList();
		}
	}
}

public static class DisposalTrackerExtensions
{
	public static void Guard(this DisposalTracker @this, Action body)
	{
		using var guard = @this.GetGuard();
		body();
	}

	public static T Guard<T>(this DisposalTracker @this, Func<T> body)
	{
		using var guard = @this.GetGuard();
		return body();
	}

	public static async Task GuardAsync(this DisposalTracker @this, Func<Task> body)
	{
		using var guard = @this.GetGuard();
		await body();
	}

	public static async Task<T> GuardAsync<T>(this DisposalTracker @this, Func<Task<T>> body)
	{
		using var guard = @this.GetGuard();
		return await body();
	}
}

internal enum Status : Byte { Alive, Disposing, Disposed }

internal struct InterlockedUInt32
{
	private UInt32 value;
	public UInt32 Increment() => Interlocked.Increment(ref value);
	public UInt32 Decrement() => Interlocked.Decrement(ref value);
	public UInt32 Read() => Interlocked.CompareExchange(ref value, 0, 0);
	public override String ToString() => Read().ToString();
}

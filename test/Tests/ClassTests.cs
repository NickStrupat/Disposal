using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disposal;

using Xunit;

public class DisposalTrackerTests
{
	private class TestDisposable : IDisposable
	{
		public bool Disposed { get; private set; }
		public void Dispose() => Disposed = true;
	}

	private class TestAsyncDisposable : IAsyncDisposable
	{
		public bool Disposed { get; private set; }
		public ValueTask DisposeAsync()
		{
			Disposed = true;
			return ValueTask.CompletedTask;
		}
	}

	private class TestClass : IAsyncDisposable
	{
		public TestDisposable Disposable = new();
		public TestAsyncDisposable AsyncDisposable = new();
		private readonly DisposalTracker tracker;
		public TestClass() => tracker = new(this);
		public ValueTask DisposeAsync() => tracker.DisposeAsync();
		public void UseGuard() => tracker.Guard(() => { });
		public T UseGuard<T>(Func<T> body) => tracker.Guard(body);
		public Task UseGuardAsync(Func<Task> body) => tracker.GuardAsync(body);
		public Task<T> UseGuardAsync<T>(Func<Task<T>> body) => tracker.GuardAsync(body);
	}

	[Fact]
	public async Task DisposeAsync_DisposesFields()
	{
		var obj = new TestClass();
		await obj.DisposeAsync();
		Assert.True(obj.Disposable.Disposed);
		Assert.True(obj.AsyncDisposable.Disposed);
	}

	[Fact]
	public async Task Guard_ThrowsAfterDispose()
	{
		var obj = new TestClass();
		await obj.DisposeAsync();
		Assert.Throws<ObjectDisposedException>(() => obj.UseGuard());
	}

	[Fact]
	public void Guard_AllowsUseWhenAlive()
	{
		var obj = new TestClass();
		Exception? ex = Record.Exception(() => obj.UseGuard());
		Assert.Null(ex);
	}

	[Fact]
	public void Guard_ReturnsValue()
	{
		var obj = new TestClass();
		var result = obj.UseGuard(() => 42);
		Assert.Equal(42, result);
	}

	[Fact]
	public async Task GuardAsync_RunsBody()
	{
		var obj = new TestClass();
		var ran = false;
		await obj.UseGuardAsync(async () =>
		{
			await Task.Yield();
			ran = true;
		});
		Assert.True(ran);
	}

	[Fact]
	public async Task GuardAsync_ReturnsValue()
	{
		var obj = new TestClass();
		var result = await obj.UseGuardAsync(async () =>
		{
			await Task.Yield();
			return "hello";
		});
		Assert.Equal("hello", result);
	}

	[Fact]
	public async Task GuardAsync_ThrowsAfterDispose()
	{
		var obj = new TestClass();
		await obj.DisposeAsync();
		await Assert.ThrowsAsync<ObjectDisposedException>(() => obj.UseGuardAsync(async () => await Task.Yield()));
	}

	[Fact]
	public async Task DisposeAsync_WaitsForActiveGuard()
	{
		var obj = new TestClass();
		var guardEntered = new TaskCompletionSource();
		var guardCanFinish = new TaskCompletionSource();
		var disposeCompleted = false;

		var guardTask = obj.UseGuardAsync(async () =>
		{
			guardEntered.SetResult();
			await guardCanFinish.Task;
		});

		await guardEntered.Task;

		var disposeTask = Task.Run(async () =>
		{
			await obj.DisposeAsync();
			disposeCompleted = true;
		});

		await Task.Delay(100);
		Assert.False(disposeCompleted);

		guardCanFinish.SetResult();
		await guardTask;
		await disposeTask;

		Assert.True(disposeCompleted);
		Assert.True(obj.Disposable.Disposed);
	}

	[Fact]
	public async Task DisposeAsync_WaitsForMultipleActiveGuards()
	{
		var obj = new TestClass();
		var guard1Entered = new TaskCompletionSource();
		var guard2Entered = new TaskCompletionSource();
		var guardsCanFinish = new TaskCompletionSource();

		var guard1Task = obj.UseGuardAsync(async () =>
		{
			guard1Entered.SetResult();
			await guardsCanFinish.Task;
		});

		var guard2Task = obj.UseGuardAsync(async () =>
		{
			guard2Entered.SetResult();
			await guardsCanFinish.Task;
		});

		await guard1Entered.Task;
		await guard2Entered.Task;

		var disposeCompleted = false;
		var disposeTask = Task.Run(async () =>
		{
			await obj.DisposeAsync();
			disposeCompleted = true;
		});

		await Task.Delay(100);
		Assert.False(disposeCompleted);

		guardsCanFinish.SetResult();
		await guard1Task;
		await guard2Task;
		await disposeTask;

		Assert.True(disposeCompleted);
		Assert.True(obj.Disposable.Disposed);
		Assert.True(obj.AsyncDisposable.Disposed);
	}

	private class ClassWithIgnoredField : IAsyncDisposable
	{
		[DisposalIgnore]
		public TestDisposable Ignored = new();
		public TestDisposable NotIgnored = new();
		private readonly DisposalTracker tracker;
		public ClassWithIgnoredField() => tracker = new(this);
		public ValueTask DisposeAsync() => tracker.DisposeAsync();
	}

	[Fact]
	public async Task DisposalIgnore_PreventsFieldDisposal()
	{
		var obj = new ClassWithIgnoredField();
		await obj.DisposeAsync();
		Assert.False(obj.Ignored.Disposed);
		Assert.True(obj.NotIgnored.Disposed);
	}

	private class ClassWithNonDisposableFields : IAsyncDisposable
	{
		public string Text = "hello";
		public int Number = 42;
		public TestDisposable Disposable = new();
		private readonly DisposalTracker tracker;
		public ClassWithNonDisposableFields() => tracker = new(this);
		public ValueTask DisposeAsync() => tracker.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_IgnoresNonDisposableFields()
	{
		var obj = new ClassWithNonDisposableFields();
		await obj.DisposeAsync();
		Assert.Equal("hello", obj.Text);
		Assert.Equal(42, obj.Number);
		Assert.True(obj.Disposable.Disposed);
	}

	private class ClassWithAutoProperty : IAsyncDisposable
	{
		public TestDisposable AutoProp { get; set; } = new();
		private readonly DisposalTracker tracker;
		public ClassWithAutoProperty() => tracker = new(this);
		public ValueTask DisposeAsync() => tracker.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_DisposesAutoPropertyBackingFields()
	{
		var obj = new ClassWithAutoProperty();
		var prop = obj.AutoProp;
		await obj.DisposeAsync();
		Assert.True(prop.Disposed);
	}

	private class ClassWithMixedDisposables : IAsyncDisposable
	{
		public TestDisposable SyncField = new();
		public TestAsyncDisposable AsyncField = new();
		public TestDisposable AnotherSyncField = new();
		private readonly DisposalTracker tracker;
		public ClassWithMixedDisposables() => tracker = new(this);
		public ValueTask DisposeAsync() => tracker.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_DisposesAllMixedDisposableFields()
	{
		var obj = new ClassWithMixedDisposables();
		await obj.DisposeAsync();
		Assert.True(obj.SyncField.Disposed);
		Assert.True(obj.AsyncField.Disposed);
		Assert.True(obj.AnotherSyncField.Disposed);
	}

	private class ClassWithPrivateDisposable : IAsyncDisposable
	{
		private TestDisposable secret = new();
		private readonly DisposalTracker tracker;
		public ClassWithPrivateDisposable() => tracker = new(this);
		public ValueTask DisposeAsync() => tracker.DisposeAsync();
		public bool IsSecretDisposed => secret.Disposed;
	}

	[Fact]
	public async Task DisposeAsync_DisposesPrivateFields()
	{
		var obj = new ClassWithPrivateDisposable();
		await obj.DisposeAsync();
		Assert.True(obj.IsSecretDisposed);
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		var obj = new TestClass();
		await obj.DisposeAsync();
		await obj.DisposeAsync();
		Assert.True(obj.Disposable.Disposed);
	}

	[Fact]
	public async Task ConcurrentGuardsAndDisposal_FieldsNotDisposedWhileGuardActive()
	{
		for (var iteration = 0; iteration < 1000; iteration++)
		{
			var obj = new TestClass();
			var fieldWasDisposedInsideGuard = false;
			var barrier = new Barrier(2);

			var guardTask = Task.Run(() =>
			{
				barrier.SignalAndWait();
				try
				{
					obj.UseGuard<bool>(() =>
					{
						if (obj.Disposable.Disposed)
							fieldWasDisposedInsideGuard = true;
						return true;
					});
				}
				catch (ObjectDisposedException) { }
			});

			var disposeTask = Task.Run(async () =>
			{
				barrier.SignalAndWait();
				await obj.DisposeAsync();
			});

			await Task.WhenAll(guardTask, disposeTask);
			Assert.False(fieldWasDisposedInsideGuard);
			Assert.True(obj.Disposable.Disposed);
		}
	}

	[Fact]
	public async Task ManyThreadsEnteringGuards_DisposalWaitsForAll()
	{
		var obj = new TestClass();
		var guardCount = 32;
		var guardsEntered = new CountdownEvent(guardCount);
		var guardsCanFinish = new TaskCompletionSource();

		Task[] guardTasks = Enumerable.Range(0, guardCount).Select(_ => Task.Run(async () =>
		{
			await obj.UseGuardAsync(async () =>
			{
				guardsEntered.Signal();
				await guardsCanFinish.Task;
			});
		})).ToArray();

		guardsEntered.Wait();

		var disposeCompleted = false;
		var disposeTask = Task.Run(async () =>
		{
			await obj.DisposeAsync();
			disposeCompleted = true;
		});

		await Task.Delay(100);
		Assert.False(disposeCompleted);

		guardsCanFinish.SetResult();
		await Task.WhenAll(guardTasks.AsEnumerable());
		await disposeTask;

		Assert.True(disposeCompleted);
		Assert.True(obj.Disposable.Disposed);
	}

	[Fact]
	public async Task RapidGuardEntryAndExit_UnderConcurrentDisposal()
	{
		for (var iteration = 0; iteration < 100; iteration++)
		{
			var obj = new TestClass();
			var cts = new CancellationTokenSource();
			var guardCallCount = 0;

			Task[] guardTasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
			{
				while (!cts.IsCancellationRequested)
				{
					try
					{
						obj.UseGuard();
						Interlocked.Increment(ref guardCallCount);
					}
					catch (ObjectDisposedException)
					{
						break;
					}
				}
			})).ToArray();

			await Task.Delay(1);
			await obj.DisposeAsync();
			cts.Cancel();
			await Task.WhenAll(guardTasks.AsEnumerable());

			Assert.True(obj.Disposable.Disposed);
			Assert.True(obj.AsyncDisposable.Disposed);
		}
	}
}
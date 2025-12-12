using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Disposal;
using Microsoft.Win32.SafeHandles;

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
}
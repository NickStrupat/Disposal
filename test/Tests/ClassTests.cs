using System;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.Win32.SafeHandles;

using Xunit;

namespace Disposal.Tests {
	public class ClassTests {
		[Fact]
		public void BasicTest() {
			var fooBarDisposed = false;
			using (var foo = new Foo()) {
				foo.DoIt2();
				foo.Bar.Disposing = () => fooBarDisposed = true;
			}
			Assert.Equal(true, fooBarDisposed);
		}

#if !NO_SPAN
		[Fact]
		public void BasicTestUsingGuard()
		{
			var fooBarDisposed = false;
			using (var foo = new Foo())
			{
				foo.DoIt4();
				foo.Bar.Disposing = () => fooBarDisposed = true;
			}
			Assert.Equal(true, fooBarDisposed);
		}
#endif
	}

	public class Woo : IDisposable {
		void IDisposable.Dispose() => Console.WriteLine(nameof(Woo) + ".IDisposable.Dispose()");
	}

	struct Bar : IDisposable {
		public Action Disposing;
		void IDisposable.Dispose() => Disposing();
	}

	internal class Foo : IDisposable {
		private Woo woo = new Woo();
		private IDisposable thing;
		public Bar Bar;
		private SafeHandle okay = new SafeWaitHandle(IntPtr.Zero, ownsHandle:true);

		private DisposalTracker<Foo> disposalTracker;
		public void Dispose() => disposalTracker.Dispose(this);

		private static void Dispose(Foo @this) {
			((IDisposable) Interlocked.Exchange(ref @this.woo, null))?.Dispose();
			Interlocked.Exchange(ref @this.thing, null)?.Dispose();
			Interlocked.Exchange(ref @this.okay, null)?.Dispose();
			//@this.Bar.Dispose();
			//@this.Bar.Dispose();
		}

		public Bar DoIt() => disposalTracker.Guard(() => {
			return this.Bar;
		});

		public void DoIt2() => disposalTracker.Guard(() => {
			DoIt();
			DoIt3(ref Bar);
		});

		private void DoIt3(ref Bar bar) {
			try {
				disposalTracker.EnterGuard();

				Console.WriteLine("");
			}
			finally {
				disposalTracker.ExitGuard();
			}
		}

#if !NO_SPAN
		public void DoIt4()
		{
			using (disposalTracker.Guard())
			Console.WriteLine("");
		}
#endif
	}
}
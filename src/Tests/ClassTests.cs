using System;
using System.Runtime.InteropServices;
using System.Threading;

using Xunit;

namespace Disposal.Tests {
	public class ClassTests {
		[Fact]
		public void BasicTest() {
			var fooBarDisposed = false;
			using (var foo = new Foo()) {
				foo.Bar.Disposing = () => fooBarDisposed = true;
			}
			Assert.Equal(true, fooBarDisposed);
		}
	}

	struct Bar : IDisposable {
		public Action Disposing;
		public void Dispose() => Disposing();
	}

	internal class Foo : IDisposable {
		private IDisposable thing;
		public Bar Bar;
		private SafeHandle okay;

		private DisposableTracker<Foo> disposableTracker;

		public void Dispose() => disposableTracker.Dispose(this);

		~Foo() { disposableTracker.Finalize(this); }

		private static void Dispose(Foo @this) {
			Interlocked.Exchange(ref @this.thing, null)?.Dispose();
			(Interlocked.Exchange(ref @this.okay, null) as IDisposable)?.Dispose();
			@this.Bar.Dispose();
			@this.Bar.Dispose();
		}

		public Bar DoIt() => disposableTracker.WrapWithIsDisposedCheck(() => {
			return this.Bar;
		});
	}
}
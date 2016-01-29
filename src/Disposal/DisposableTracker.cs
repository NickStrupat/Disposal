using System;
using System.Threading;

namespace Disposal {
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
	public struct DisposableTracker {
		private Int32 useCount;

		private void Enter() {
			if (Interlocked.Increment(ref useCount) < 0)
				throw new ObjectDisposedException(GetType().FullName);
		}

		private void Exit() => Interlocked.Decrement(ref useCount);

		private enum State { Disposed, InUse, Disposable }

		private State TryMarkDispose() {
			var x = Interlocked.CompareExchange(ref useCount, -1, 0);
			switch (x) {
				case 0:
					return State.Disposable;
				case -1:
					return State.Disposed;
			}
			return State.InUse;
		}

		private Boolean IsDisposed() {
			for (;;) {
				var state = TryMarkDispose();
				switch (state) {
					case State.Disposed:
						return true;
					case State.InUse:
						continue;
					case State.Disposable:
						return false;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		public static void Dispose<T>(ref DisposableTracker disposableTracker, T disposable) where T : class, IDisposable {
			if (disposableTracker.IsDisposed())
				return;
			DisposalInternals.ClassDisposerCache<T>.Dispose(disposable);
		}

		public static void Finalize<T>(ref DisposableTracker disposableTracker, T disposable) where T : class, IDisposable {
			Dispose(ref disposableTracker, disposable);
			GC.SuppressFinalize(disposable);
		}

		public static void DisposeStruct<T>(ref DisposableTracker disposableTracker, ref T disposable) where T : struct, IDisposable {
			if (disposableTracker.IsDisposed())
				return;
			DisposalInternals.StructDisposerCache<T>.Dispose(ref disposable);
		}

		public T WrapWithIsDisposedCheck<T>(Func<T> body) {
			try {
				Enter();
				return body();
			}
			finally {
				Exit();
			}
		}

		public void WrapWithIsDisposedCheck(Action body) {
			try {
				Enter();
				body();
			}
			finally {
				Exit();
			}
		}
	}
}
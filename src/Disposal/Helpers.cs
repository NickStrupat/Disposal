using System;
using System.Threading;

namespace Disposal {
	internal static class Helpers {
		private const Int32 Disposed = Int32.MinValue;
		private const Int32 Disposable = 0;

		public static void Enter(ref Int32 useCount) {
			if (Interlocked.Increment(ref useCount) > 0)
				return;
			Interlocked.Decrement(ref useCount);
			throw new ObjectDisposedException(null);
		}

		public static void Exit(ref Int32 useCount) {
			Interlocked.Decrement(ref useCount);
		}

		public static Boolean IsDisposed(ref Int32 useCount) {
			for (;;) {
				switch (Interlocked.CompareExchange(ref useCount, Disposed, Disposable)) {
					case Disposed:
						return true;
					case Disposable:
						return false;
				}
			}
		}

		public static TResult WrapWithDisposalCheck<TResult>(ref Int32 useCount, Func<TResult> body) {
			try {
				Enter(ref useCount);
				return body();
			}
			finally {
				Exit(ref useCount);
			}
		}

		public static void WrapWithDisposalCheck(ref Int32 useCount, Action body) {
			try {
				Enter(ref useCount);
				body();
			}
			finally {
				Exit(ref useCount);
			}
		}
	}
}
using System;
using System.Threading;

namespace Disposal {
	internal static class Helpers {
		private const Int32 Disposed = Int32.MinValue;
		private const Int32 Disposable = 0;

		public static void Enter(ref Int32 useCount) {
			if (Interlocked.Increment(ref useCount) <= 0)
				throw new ObjectDisposedException(null);
		}

		public static void Exit(ref Int32 useCount) {
			Interlocked.Decrement(ref useCount);
		}

		public static Boolean MarkDisposed(ref Int32 useCount) {
			var spinWait = new SpinWait();
			for (;;) {
				var original = Interlocked.CompareExchange(ref useCount, Disposed, Disposable); // set useCount to Disposed if is currently Disposable
				if (original == Disposable)
					return true;
				// Check if less-than 0 because a user could mess up the calls to EnterGuard and ExitGuard by not correctly putting them in
				// a try-finally block (see DisposalGuard methods). This gives us room for (Int.MaxValue - 1) bad calls to Enter/ExitGuard
				if (original < Disposable)
					return false;
				spinWait.SpinOnce();
			}
		}

		public static TResult DisposalGuard<TResult>(ref Int32 useCount, Func<TResult> body) {
			try {
				Enter(ref useCount);
				return body();
			}
			finally {
				Exit(ref useCount);
			}
		}

		public static void DisposalGuard(ref Int32 useCount, Action body) {
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
using System;

namespace Disposal {
	public struct DisposableStructTracker<T> where T : struct, IDisposable {
		private Int32 useCount;

		public void Dispose(ref T disposable) {
			if (Helpers.MarkDisposed(ref useCount))
				DisposalInternals.StructDisposerCache<T>.Dispose(ref disposable);
		}

		public TResult Guard<TResult>(Func<TResult> body) {
			if (body == null)
				throw new ArgumentNullException(nameof(body));
			return Helpers.DisposalGuard(ref useCount, body);
		}

		public void Guard(Action body) {
			if (body == null)
				throw new ArgumentNullException(nameof(body));
			Helpers.DisposalGuard(ref useCount, body);
		}

		public void EnterGuard() => Helpers.Enter(ref useCount);
		public void ExitGuard() => Helpers.Exit(ref useCount);
	}
}
using System;

namespace Disposal {
	public struct DisposableTracker<T> where T : class, IDisposable {
		private Int32 useCount;

		public void Dispose(T disposable) {
			if (disposable == null)
				throw new ArgumentNullException(nameof(disposable));
			if (Helpers.MarkDisposed(ref useCount))
				DisposalInternals.ClassDisposerCache<T>.Dispose(disposable);
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
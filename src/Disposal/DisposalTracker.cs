using System;
using System.Runtime.InteropServices;

namespace Disposal {
	public struct DisposalTracker<T> where T : class, IDisposable {
		private Int32 useCount;

		public void Dispose(T disposable) {
			if (disposable == null)
				throw new ArgumentNullException(nameof(disposable));
			if (Helpers.MarkDisposed(ref useCount))
				DisposalInternals.ClassDisposerCache<T>.Dispose(disposable);
		}

#if !NO_SPAN
		public Guard<T> Guard() => new Guard<T>(MemoryMarshal.CreateSpan(ref this, 1));
#endif

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

#if !NO_SPAN
	public ref struct Guard<T> where T : class, IDisposable
	{
		private readonly Span<DisposalTracker<T>> span;
		internal Guard(Span<DisposalTracker<T>> disposalTracker) => (span = disposalTracker)[0].EnterGuard();
		public void Dispose() => span[0].ExitGuard();
	}
#endif
}
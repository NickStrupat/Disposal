using System;

namespace Disposal {
	public struct DisposableStructTracker<T> where T : struct, IDisposable {
		private Int32 useCount;

		public void Dispose(ref T disposable) {
			if (Helpers.IsDisposed(ref useCount))
				return;
			DisposalInternals.StructDisposerCache<T>.Dispose(ref disposable);
		}

		public TResult WrapWithIsDisposedCheck<TResult>(Func<TResult> body) {
			if (body == null)
				throw new ArgumentNullException(nameof(body));
			return Helpers.WrapWithDisposalCheck(ref useCount, body);
		}

		public void WrapWithIsDisposedCheck(Action body) {
			if (body == null)
				throw new ArgumentNullException(nameof(body));
			Helpers.WrapWithDisposalCheck(ref useCount, body);
		}
	}
}
﻿using System;

namespace Disposal {
	public struct DisposableTracker<T> where T : class, IDisposable {
		private Int32 useCount;

		public void Dispose(T disposable) {
			if (disposable == null)
				throw new ArgumentNullException(nameof(disposable));
			if (Helpers.IsDisposed(ref useCount))
				return;
			DisposalInternals.ClassDisposerCache<T>.Dispose(disposable);
		}

		public void Finalize(T disposable) {
			Dispose(disposable);
			GC.SuppressFinalize(disposable);
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
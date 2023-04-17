﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;

namespace Codist.QuickInfo
{
	sealed class QuickInfoOverrideController : IAsyncQuickInfoSource
	{
		public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			return QuickInfoOverrider.CheckCtrlSuppression()
				? null
				: new QuickInfoItem(null, QuickInfoOverrider.CreateOverrider(session).CreateControl(session));
		}

		void IDisposable.Dispose() {}
	}
}
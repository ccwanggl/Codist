﻿using System;
using System.Threading;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using R = Codist.Properties.Resources;

namespace Codist.SmartBars
{
	sealed class MarkdownSmartBar : SmartBar
	{
		public MarkdownSmartBar(IWpfTextView textView, Microsoft.VisualStudio.Text.Operations.ITextSearchService2 textSearchService) : base(textView, textSearchService) {
		}

		ToolBar MyToolBar => ToolBar2;

		protected override void AddCommands(CancellationToken cancellationToken) {
			AddCommand(MyToolBar, IconIds.TagBold, R.CMD_MarkBold, ctx => WrapWith(ctx, "**", "**", true));
			AddCommand(MyToolBar, IconIds.TagItalic, R.CMD_MarkItalic, ctx => WrapWith(ctx, "_", "_", true));
			AddCommand(MyToolBar, IconIds.TagCode, R.CMD_MarkCode, ctx => WrapWith(ctx, "`", "`", true));
			AddCommand(MyToolBar, IconIds.TagHyperLink, R.CMD_MarkLink, MakeUrl);
			AddCommand(MyToolBar, IconIds.TagStrikeThrough, R.CMD_MarkStrikeThrough, ctx => WrapWith(ctx, "~~", "~~", true));
			base.AddCommands(cancellationToken);
		}

		void MakeUrl(CommandContext ctx) {
			var t = ctx.View.GetFirstSelectionText();
			if (MaybeUrl(t)) {
				var s = WrapWith(ctx, "[title](", ")", false);
				if (s.Snapshot != null) {
					// select the "title"
					ctx.View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start + 1, 5), false);
					ctx.View.Caret.MoveTo(s.Start + 6);
				}
			}
			else {
				string clip;
				try {
					clip = System.Windows.Clipboard.GetText();
				}
				catch (SystemException) {
					// ignore
					clip = null;
				}
				var s = MaybeUrl(clip) && clip.IndexOf(')') < 0
					? WrapWith(ctx, "[", "](" + clip + ")", false)
					: WrapWith(ctx, "[", "](url)", false);
				if (s.Snapshot != null) {
					// select the "url"
					if (clip != null) {
						ctx.View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start + s.Length - clip.Length - 1, clip.Length), false);
					}
					else {
						ctx.View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start + s.Length - 4, 3), false);
					}
					ctx.View.Caret.MoveTo(s.End - 1);
				}
			}
		}

		static bool MaybeUrl(string text) {
			return text?.Length > 7
				&& (text.StartsWith("http://", StringComparison.Ordinal) || text.StartsWith("https://", StringComparison.Ordinal));
		}
	}
}

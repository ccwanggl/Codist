﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using CLR;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;
using R = Codist.Properties.Resources;

namespace Codist.Commands
{
	internal abstract class AutoSurroundSelectionCommandBase : ICommandHandler<TypeCharCommandArgs>
	{
		static readonly Dictionary<char, string> __CommonPairs = new Dictionary<char, string>() {
			{ '\'', "'" },
			{ '"', "\"" },
			{ '(', ")" },
			{ '{', "}" },
			{ '[', "]" },
			{ '<', ">" },
		};
		static readonly Dictionary<char, string> __TextPairs = IsChineseEnvironment() == false ? __CommonPairs
			: new Dictionary<char, string>(__CommonPairs) {
				{ '\u201C', "\u201D" }, // full-width quotation mark ""
				{ '\u2018', "\u2019" }, // full-width single quotation mark ''
				{ '\uFF08', "\uFF09" }, // full-width parentheses ()
				{ '\uFF5B', "\uFF5D" }, // full-width square bracket []
				{ '\uFF1C', "\uFF1E" }, // full-width less than / greater than sign <>
				{ '\uFF62', "\uFF63" }, // corner bracket ""
				{ '\u3010', "\u3011" }, // black lenticular bracket []
				{ '\u3014', "\u3015" }, // tortoise shell bracket []
				{ '\u3016', "\u3017" }, // white lenticular bracket []
				{ '\u300A', "\u300B" }, // double angle bracket <<>>
				{ '\u3008', "\u3009" }, // single angle bracket <>
				{ '\u300C', "\u300D" }, // corner bracket
				{ '\u300E', "\u300F" }, // white corner bracket
			};

		static bool IsChineseEnvironment() {
			return System.Globalization.CultureInfo.CurrentCulture.LCID.CeqAny(2052, 3076, 5124, 1028, 4100)
				|| R.Culture.LCID.CeqAny(2052, 3076, 5124, 1028, 4100);
		}

		protected abstract Dictionary<char, string> TextPairs { get; }

		public abstract string DisplayName { get; }

		public bool ExecuteCommand(TypeCharCommandArgs args, CommandExecutionContext executionContext) {
			return Config.Instance.Features.MatchFlags(Features.AutoSurround) && ProcessChar(args);
		}

		bool ProcessChar(TypeCharCommandArgs args) {
			ITextView view = args.TextView;
			if (view.Selection.IsEmpty || TextPairs.TryGetValue(args.TypedChar, out var endText) == false) {
				return false;
			}

			ITextSnapshot newText;
			var oldSpans = view.Selection.SelectedSpans;
			var startText = args.TypedChar.ToString();
			var history = ServicesHelper.Instance.TextUndoHistoryService.GetHistory(view.TextBuffer);
			var trim = Config.Instance.AutoSurroundSelectionOptions.MatchFlags(AutoSurroundSelectionOptions.Trim);
			using (var transaction = history.CreateTransaction(startText + R.T_AutoSurround + endText))
			using (var edit = view.TextBuffer.CreateEdit()) {
				foreach (var span in oldSpans) {
					if (trim
						&& span.IsEmpty == false
						&& TryTrimSpan(span, out var trimStart, out var trimEnd)) {
						edit.Delete(Span.FromBounds(span.Start.Position, trimStart));
						edit.Delete(Span.FromBounds(trimEnd, span.End.Position));
					}
					edit.Insert(span.Start, startText);
					edit.Insert(span.End, endText);
				}
				newText = edit.Apply();
				transaction.Complete();
			}
			view.GetMultiSelectionBroker().AddSelectionRange(UpdateSelections(oldSpans, newText));
			return true;
		}

		static bool TryTrimSpan(SnapshotSpan span, out int trimStart, out int trimEnd) {
			var s = span.Snapshot;
			var to = span.End.Position;
			var from = span.Start.Position;
			while (from < to) {
				if (s[from].CeqAny(' ', '\t')) {
					++from;
				}
				else {
					break;
				}
			}
			trimStart = from;
			--to;
			while (from < to) {
				if (s[to].CeqAny(' ', '\t')) {
					--to;
				}
				else {
					break;
				}
			}
			trimEnd = to + 1;
			return from != span.Start.Position || trimEnd != span.End.Position;
		}

		public CommandState GetCommandState(TypeCharCommandArgs args) {
			return CommandState.Available;
		}

		static IEnumerable<Selection> UpdateSelections(IEnumerable<SnapshotSpan> oldSpans, ITextSnapshot newText) {
			foreach (var item in oldSpans) {
				var extent = item.TranslateTo(newText, SpanTrackingMode.EdgeInclusive);
				yield return new Selection(extent.Length > 1
					? new SnapshotSpan(newText, extent.Start + 1, extent.Length - 1)
					: extent);
			}
		}

		[Export(typeof(ICommandHandler))]
		[Name(nameof(AutoSurroundSelectionCommandBase) + "." + nameof(TextAndCode))]
		[ContentType(Constants.CodeTypes.PlainText)]
		[ContentType(Constants.CodeTypes.Code)]
		[TextViewRole(PredefinedTextViewRoles.Document)]
		[TextViewRole(PredefinedTextViewRoles.Editable)]
		sealed class TextAndCode : AutoSurroundSelectionCommandBase
		{
			public override string DisplayName => "TextAutoSurroundCommand";
			protected override Dictionary<char, string> TextPairs => __TextPairs;
		}

		[Export(typeof(ICommandHandler))]
		[Name(nameof(AutoSurroundSelectionCommandBase) + "." + nameof(Markdown))]
		[ContentType(Constants.CodeTypes.Markdown)]
		[ContentType("code++.Markdown")]
		[ContentType(Constants.CodeTypes.VsMarkdown)]
		[TextViewRole(PredefinedTextViewRoles.Document)]
		[TextViewRole(PredefinedTextViewRoles.Editable)]
		sealed class Markdown : AutoSurroundSelectionCommandBase
		{
			static readonly Dictionary<char, string> __MarkdownPairs = new Dictionary<char, string>(__TextPairs) {
				{ '`', "`" },
				{ '*', "*" },
				{ '_', "_" },
				{ '~', "~" },
			};
			public override string DisplayName => "MarkdownAutoSurroundCommand";
			protected override Dictionary<char, string> TextPairs => __MarkdownPairs;
		}
	}
}

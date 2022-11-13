﻿using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Text;

namespace Codist.Refactorings
{
	abstract class ReplaceToken
	{
		public static readonly ReplaceToken InvertOperator = new InvertOperatorRefactoring();

		public abstract bool AcceptToken(SyntaxToken token);
		protected abstract string GetReplacement(SyntaxToken token);

		public void Refactor(SemanticContext ctx) {
			var view = ctx.View;
			var token = ctx.Token;
			var rep = GetReplacement(token);
			view.Edit(
				(rep, sel: token.Span.ToSpan()),
				(v, p, edit) => edit.Replace(p.sel, p.rep)
			);
			view.Caret.MoveTo(new SnapshotPoint(view.TextSnapshot, token.SpanStart));
			view.Selection.Select(new SnapshotSpan(view.TextSnapshot, token.SpanStart, rep.Length), false);
		}

		sealed class InvertOperatorRefactoring : ReplaceToken
		{
			public override bool AcceptToken(SyntaxToken token) {
				switch (token.Kind()) {
					case SyntaxKind.EqualsEqualsToken:
					case SyntaxKind.ExclamationEqualsToken:
					case SyntaxKind.AmpersandAmpersandToken:
					case SyntaxKind.BarBarToken:
					case SyntaxKind.MinusMinusToken:
					case SyntaxKind.PlusPlusToken:
					case SyntaxKind.LessThanToken:
					case SyntaxKind.GreaterThanToken:
					case SyntaxKind.LessThanEqualsToken:
					case SyntaxKind.GreaterThanEqualsToken:
					case SyntaxKind.PlusToken:
					case SyntaxKind.MinusToken:
					case SyntaxKind.AsteriskToken:
					case SyntaxKind.SlashToken:
					case SyntaxKind.AmpersandToken:
					case SyntaxKind.BarToken:
					case SyntaxKind.LessThanLessThanToken:
					case SyntaxKind.GreaterThanGreaterThanToken:
					case SyntaxKind.PlusEqualsToken:
					case SyntaxKind.MinusEqualsToken:
					case SyntaxKind.AsteriskEqualsToken:
					case SyntaxKind.SlashEqualsToken:
					case SyntaxKind.LessThanLessThanEqualsToken:
					case SyntaxKind.GreaterThanGreaterThanEqualsToken:
					case SyntaxKind.AmpersandEqualsToken:
					case SyntaxKind.BarEqualsToken:
						return true;
				}
				return false;
			}

			protected override string GetReplacement(SyntaxToken token) {
				switch (token.Kind()) {
					case SyntaxKind.EqualsEqualsToken: return "!=";
					case SyntaxKind.ExclamationEqualsToken: return "==";
					case SyntaxKind.AmpersandAmpersandToken: return "||";
					case SyntaxKind.BarBarToken: return "&&";
					case SyntaxKind.MinusMinusToken: return "++";
					case SyntaxKind.PlusPlusToken: return "--";
					case SyntaxKind.LessThanToken: return ">=";
					case SyntaxKind.GreaterThanToken: return "<=";
					case SyntaxKind.LessThanEqualsToken: return ">";
					case SyntaxKind.GreaterThanEqualsToken: return "<";
					case SyntaxKind.PlusToken: return "-";
					case SyntaxKind.MinusToken: return "+";
					case SyntaxKind.AsteriskToken: return "/";
					case SyntaxKind.SlashToken: return "*";
					case SyntaxKind.AmpersandToken: return "|";
					case SyntaxKind.BarToken: return "&";
					case SyntaxKind.LessThanLessThanToken: return ">>";
					case SyntaxKind.GreaterThanGreaterThanToken: return "<<";
					case SyntaxKind.PlusEqualsToken: return "-=";
					case SyntaxKind.MinusEqualsToken: return "+=";
					case SyntaxKind.AsteriskEqualsToken: return "/=";
					case SyntaxKind.SlashEqualsToken: return "*=";
					case SyntaxKind.LessThanLessThanEqualsToken: return ">>=";
					case SyntaxKind.GreaterThanGreaterThanEqualsToken: return "<<=";
					case SyntaxKind.AmpersandEqualsToken: return "|=";
					case SyntaxKind.BarEqualsToken: return "&=";
				}
				return null;
			}
		}
	}
}
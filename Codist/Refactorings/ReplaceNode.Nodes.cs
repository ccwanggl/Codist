﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using R = Codist.Properties.Resources;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Codist.Refactorings
{
	abstract partial class ReplaceNode
	{
		public static readonly ReplaceNode AddBraces = new AddBracesRefactoring();
		public static readonly ReplaceNode AsToCast = new AsToCastRefactoring();
		public static readonly ReplaceNode SealClass = new SealClassRefactoring();
		public static readonly ReplaceNode MakePublic = new ChangeAccessibilityRefactoring(SyntaxKind.PublicKeyword);
		public static readonly ReplaceNode MakeProtected = new ChangeAccessibilityRefactoring(SyntaxKind.ProtectedKeyword);
		public static readonly ReplaceNode MakeInternal = new ChangeAccessibilityRefactoring(SyntaxKind.InternalKeyword);
		public static readonly ReplaceNode MakePrivate = new ChangeAccessibilityRefactoring(SyntaxKind.PrivateKeyword);
		public static readonly ReplaceNode RemoveContainingStatement = new RemoveContainerRefactoring();
		public static readonly ReplaceNode SwapOperands = new SwapOperandsRefactoring();
		public static readonly ReplaceNode MultiLineList = new MultiLineListRefactoring();
		public static readonly ReplaceNode MultiLineExpression = new MultiLineExpressionRefactoring();
		public static readonly ReplaceNode MultiLineMemberAccess = new MultiLineMemberAccessRefactoring();
		public static readonly ReplaceNode ConcatToInterpolatedString = new ConcatToInterpolatedStringRefactoring();

		sealed class AddBracesRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.AddBraces;
			public override string Title => R.CMD_AddBraces;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				switch (node.Kind()) {
					case SyntaxKind.IfStatement:
						return ((IfStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.ForEachStatement:
						return ((ForEachStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.ForStatement:
						return ((ForStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.ForEachVariableStatement:
						return ((ForEachVariableStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.WhileStatement:
						return ((WhileStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.UsingStatement:
						return ((UsingStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.LockStatement:
						return ((LockStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.ElseClause:
						return ((ElseClauseSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.FixedStatement:
						return ((FixedStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.CaseSwitchLabel:
						node = node.Parent;
						goto case SyntaxKind.SwitchSection;
					case SyntaxKind.SwitchSection:
						var statements = ((SwitchSectionSyntax)node).Statements;
						return statements.Count != 0 && statements[0].IsKind(SyntaxKind.Block) == false;
					default: return false;
				}
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var node = ctx.Node;
				StatementSyntax statement;
				switch (node.Kind()) {
					case SyntaxKind.IfStatement:
						statement = ((IfStatementSyntax)node).Statement; break;
					case SyntaxKind.ForEachStatement:
						statement = ((ForEachStatementSyntax)node).Statement; break;
					case SyntaxKind.ForEachVariableStatement:
						statement = ((ForEachVariableStatementSyntax)node).Statement; break;
					case SyntaxKind.ForStatement:
						statement = ((ForStatementSyntax)node).Statement; break;
					case SyntaxKind.WhileStatement:
						statement = ((WhileStatementSyntax)node).Statement; break;
					case SyntaxKind.UsingStatement:
						statement = ((UsingStatementSyntax)node).Statement; break;
					case SyntaxKind.LockStatement:
						statement = ((LockStatementSyntax)node).Statement; break;
					case SyntaxKind.FixedStatement:
						statement = ((FixedStatementSyntax)node).Statement; break;
					case SyntaxKind.ElseClause:
						var oldElse = (ElseClauseSyntax)node;
						var newElse = oldElse.WithStatement(SF.Block(oldElse.Statement)).AnnotateReformatAndSelect();
						return Chain.Create(Replace(oldElse, newElse));
					case SyntaxKind.CaseSwitchLabel:
						node = node.Parent;
						goto case SyntaxKind.SwitchSection;
					case SyntaxKind.SwitchSection:
						var oldSection = (SwitchSectionSyntax)node;
						var newSection = oldSection.WithStatements(SF.SingletonList((StatementSyntax)SF.Block(oldSection.Statements))).AnnotateReformatAndSelect();
						return Chain.Create(Replace(oldSection, newSection));
					default: 
						return Enumerable.Empty<RefactoringAction>();
				}
				if (statement != null) {
					return Chain.Create(Replace(statement.Parent, statement.Parent.ReplaceNode(statement, SF.Block(statement)).AnnotateReformatAndSelect()));
				}
				return Enumerable.Empty<RefactoringAction>();
			}
		}

		sealed class AsToCastRefactoring : ReplaceNode
		{
			string _Title;
			public override int IconId => IconIds.AsToCast;
			public override string Title => _Title;

			public override bool Accept(RefactoringContext ctx) {
				switch (ctx.NodeIncludeTrivia.RawKind) {
					case (int)SyntaxKind.AsExpression:
						_Title = R.CMD_AsToCast;
						return true;
					case (int)SyntaxKind.CastExpression:
						_Title = R.CMD_CastToAs;
						return true;
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				if (ctx.NodeIncludeTrivia is BinaryExpressionSyntax exp) {
					return Chain.Create(Replace(exp, SF.CastExpression(exp.Right.WithoutTrailingTrivia() as TypeSyntax, exp.Left).WithTriviaFrom(exp).AnnotateReformatAndSelect()));
				}
				if (ctx.NodeIncludeTrivia is CastExpressionSyntax ce) {
					return Chain.Create(Replace(ce, SF.BinaryExpression(SyntaxKind.AsExpression, ce.Expression.WithoutTrailingTrivia(), ce.Type).WithTriviaFrom(ce.Expression).AnnotateReformatAndSelect()));
				}
				return Enumerable.Empty<RefactoringAction>();
			}
		}

		sealed class SealClassRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.SealedClass;
			public override string Title => "Seal Class";

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.Node;
				ClassDeclarationSyntax d;
				return node.IsKind(SyntaxKind.ClassDeclaration)
					&& CanBeSealed((d = node as ClassDeclarationSyntax).Modifiers);
			}

			static bool CanBeSealed(SyntaxTokenList modifiers) {
				foreach (var item in modifiers) {
					switch (item.Kind()) {
						case SyntaxKind.SealedKeyword:
						case SyntaxKind.AbstractKeyword:
						case SyntaxKind.StaticKeyword:
							return false;
					}
				}
				return true;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var d = ctx.Node as ClassDeclarationSyntax;
				return Chain.Create(Replace(d, d
					.WithoutLeadingTrivia()
					.WithModifiers(d.Modifiers
						.Add(SF.Token(SyntaxKind.SealedKeyword)
							.WithTrailingTrivia(SF.ElasticSpace)
							.WithAdditionalAnnotations(CodeFormatHelper.Select)))
					.WithLeadingTrivia(d.GetLeadingTrivia())));
			}
		}

		sealed class ChangeAccessibilityRefactoring : ReplaceNode
		{
			readonly SyntaxKind _KeywordKind;
			readonly int _IconId;
			readonly string _Title;

			public override int IconId => _IconId;
			public override string Title => _Title;

			public ChangeAccessibilityRefactoring(SyntaxKind accessibility) {
				switch (_KeywordKind = accessibility) {
					case SyntaxKind.PublicKeyword:
						_IconId = IconIds.PublicSymbols;
						_Title = "Make Public";
						break;
					case SyntaxKind.ProtectedKeyword:
						_IconId = IconIds.ProtectedSymbols;
						_Title = "Make Protected";
						break;
					case SyntaxKind.InternalKeyword:
						_IconId = IconIds.InternalSymbols;
						_Title = "Make Internal";
						break;
					case SyntaxKind.PrivateKeyword:
						_IconId = IconIds.PrivateSymbols;
						_Title = "Make Private";
						break;
				}
			}

			public override bool Accept(RefactoringContext ctx) {
				var node = GetDeclarationNode(ctx);
				return node != null && CanChangeAccessibility(node);
			}

			static MemberDeclarationSyntax GetDeclarationNode(RefactoringContext ctx) {
				var node = ctx.Node;
				if (node.IsKind(SyntaxKind.VariableDeclarator)) {
					node = node.Parent.Parent;
				}
				return node as MemberDeclarationSyntax;
			}

			bool CanChangeAccessibility(MemberDeclarationSyntax d) {
				var m = GetModifiers(d);
				if (m.Any(_KeywordKind)
					|| m.Any(SyntaxKind.OverrideKeyword)
					|| d.IsKind(SyntaxKind.EnumMemberDeclaration)) {
					return false;
				}
				switch (_KeywordKind) {
					case SyntaxKind.PublicKeyword:
					case SyntaxKind.InternalKeyword:
						return true;
					case SyntaxKind.ProtectedKeyword:
						return m.Any(SyntaxKind.SealedKeyword) == false
							&& d.Parent is ClassDeclarationSyntax c
							&& c.Modifiers.Any(SyntaxKind.SealedKeyword) == false;
					case SyntaxKind.PrivateKeyword:
						if (d is BaseTypeDeclarationSyntax t
							&& t.IsKind(SyntaxKind.InterfaceDeclaration) == false) {
							return d.Parent is BaseTypeDeclarationSyntax;
						}
						return true;
				}
				return true;
			}

			static SyntaxTokenList GetModifiers(MemberDeclarationSyntax d) {
				if (d is BaseTypeDeclarationSyntax t) {
					return t.Modifiers;
				}
				if (d is BaseMethodDeclarationSyntax m) {
					return m.Modifiers;
				}
				if (d is BaseFieldDeclarationSyntax f) {
					return f.Modifiers;
				}
				if (d is BasePropertyDeclarationSyntax p) {
					return p.Modifiers;
				}
				return default;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var d = GetDeclarationNode(ctx);
				if (d is BaseTypeDeclarationSyntax t) {
					return Chain.Create(Replace(d, t
						.WithoutLeadingTrivia()
						.WithModifiers(ReplaceModifiers(t.Modifiers))
						.WithLeadingTrivia(d.GetLeadingTrivia())));
				}
				if (d is BaseMethodDeclarationSyntax m) {
					return Chain.Create(Replace(d, m
						.WithoutLeadingTrivia()
						.WithModifiers(ReplaceModifiers(m.Modifiers))
						.WithLeadingTrivia(d.GetLeadingTrivia())));
				}
				if (d is BaseFieldDeclarationSyntax f) {
					return Chain.Create(Replace(d, f
						.WithoutLeadingTrivia()
						.WithModifiers(ReplaceModifiers(f.Modifiers))
						.WithLeadingTrivia(d.GetLeadingTrivia())));
				}
				if (d is BasePropertyDeclarationSyntax p) {
					return Chain.Create(Replace(d, p
						.WithoutLeadingTrivia()
						.WithModifiers(ReplaceModifiers(p.Modifiers))
						.WithLeadingTrivia(d.GetLeadingTrivia())));
				}
				return Enumerable.Empty<RefactoringAction>();
			}

			SyntaxTokenList ReplaceModifiers(SyntaxTokenList original) {
				var r = original;
				var replaced = false;
				foreach (var item in original) {
					switch (item.Kind()) {
						case SyntaxKind.PublicKeyword:
						case SyntaxKind.ProtectedKeyword:
						case SyntaxKind.InternalKeyword:
						case SyntaxKind.PrivateKeyword:
							if (replaced == false) {
								r = r.Replace(item, SF.Token(_KeywordKind).WithTriviaFrom(item).WithAdditionalAnnotations(CodeFormatHelper.Select));
								replaced = true;
							}
							else {
								r = r.Remove(item);
							}
							break;
					}
				}

				if (replaced) {
					return r;
				}
				if (original.Count == 0) {
					return r.Add(SF.Token(_KeywordKind).WithTrailingTrivia(SF.ElasticSpace).WithAdditionalAnnotations(CodeFormatHelper.Select));
				}
				return r.ReplaceRange(r[0], new[] {
						SF.Token(_KeywordKind)
							.WithLeadingTrivia(r[0].LeadingTrivia)
							.WithAdditionalAnnotations(CodeFormatHelper.Select),
						r[0].WithLeadingTrivia(SF.ElasticSpace)
					});
			}
		}

		sealed class RemoveContainerRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.Delete;
			public override string Title => R.CMD_DeleteContainingBlock;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				var s = node.GetContainingStatement();
				return s != null
					&& s.SpanStart == node.SpanStart
					&& GetRemovableAncestor(s) != null;
			}

			static bool CanBeRemoved(SyntaxNode node) {
				switch (node.Kind()) {
					case SyntaxKind.ForEachStatement:
					case SyntaxKind.ForEachVariableStatement:
					case SyntaxKind.ForStatement:
					case SyntaxKind.UsingStatement:
					case SyntaxKind.WhileStatement:
					case SyntaxKind.DoStatement:
					case SyntaxKind.LockStatement:
					case SyntaxKind.FixedStatement:
					case SyntaxKind.UnsafeStatement:
					case SyntaxKind.TryStatement:
					case SyntaxKind.CheckedStatement:
					case SyntaxKind.UncheckedStatement:
					case SyntaxKind.IfStatement:
						return true;
					case SyntaxKind.ElseClause:
						return ((ElseClauseSyntax)node).Statement?.Kind() != SyntaxKind.IfStatement;
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var statement = ctx.Node.GetContainingStatement();
				var remove = GetRemovableAncestor(statement);
				if (remove == null) {
					return Enumerable.Empty<RefactoringAction>();
				}
				SyntaxList<StatementSyntax> keep = statement.Parent is BlockSyntax b
					? b.Statements
					: new SyntaxList<StatementSyntax>(statement);
				if (remove.IsKind(SyntaxKind.ElseClause)) {
					var ifs = remove.Parent as IfStatementSyntax;
					if (ifs.Parent.IsKind(SyntaxKind.ElseClause)) {
						return Chain.Create(Replace(ifs.Parent,
							(keep.Count > 1 || statement.Parent.IsKind(SyntaxKind.Block) || keep.Count == 0
								? SF.ElseClause(SF.Block(keep))
								: SF.ElseClause(keep[0])).AnnotateReformatAndSelect()));
					}
					else {
						keep = keep.Insert(0, ifs.WithElse(null));
					}
					remove = ifs;
				}
				return Chain.Create(Replace(remove, keep.AttachAnnotation(CodeFormatHelper.Reformat, CodeFormatHelper.Select)));
			}

			static SyntaxNode GetRemovableAncestor(SyntaxNode node) {
				if (node == null) {
					return null;
				}
				do {
					if (CanBeRemoved(node = node.Parent)) {
						return node;
					}
				} while (node is StatementSyntax);
				return null;
			}
		}

		sealed class SwapOperandsRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.SwapOperands;
			public override string Title => R.CMD_SwapOperands;

			public override bool Accept(RefactoringContext ctx) {
				switch (ctx.NodeIncludeTrivia.Kind()) {
					case SyntaxKind.LogicalAndExpression:
					case SyntaxKind.LogicalOrExpression:
					case SyntaxKind.BitwiseAndExpression:
					case SyntaxKind.BitwiseOrExpression:
					case SyntaxKind.ExclusiveOrExpression:
					case SyntaxKind.EqualsExpression:
					case SyntaxKind.NotEqualsExpression:
					case SyntaxKind.LessThanExpression:
					case SyntaxKind.LessThanOrEqualExpression:
					case SyntaxKind.GreaterThanExpression:
					case SyntaxKind.GreaterThanOrEqualExpression:
					case SyntaxKind.AddExpression:
					case SyntaxKind.SubtractExpression:
					case SyntaxKind.MultiplyExpression:
					case SyntaxKind.DivideExpression:
					case SyntaxKind.ModuloExpression:
					case SyntaxKind.CoalesceExpression:
						return true;
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia as BinaryExpressionSyntax;
				ExpressionSyntax right = node.Right, left = node.Left;
				if (left == null || right == null) {
					return Enumerable.Empty<RefactoringAction>();
				}

				#region Swap operands besides selected operator
				if (Keyboard.Modifiers.MatchFlags(ModifierKeys.Shift) == false) {
					BinaryExpressionSyntax temp;
					if ((temp = left as BinaryExpressionSyntax) != null
						&& temp.RawKind == node.RawKind
						&& temp.Right != null) {
						left = temp.Right;
						right = temp.Update(temp.Left, temp.OperatorToken, right);
					}
					else if ((temp = right as BinaryExpressionSyntax) != null
						&& temp.RawKind == node.RawKind
						&& temp.Left != null) {
						left = temp.Update(left, temp.OperatorToken, temp.Right);
						right = temp.Left;
					}
				}
				#endregion

				var newNode = node.Update(right.WithTrailingTrivia(left.GetTrailingTrivia()),
					node.OperatorToken,
					right.HasTrailingTrivia && right.GetTrailingTrivia().Last().IsKind(SyntaxKind.EndOfLineTrivia)
						? left.WithLeadingTrivia(right.GetLeadingTrivia())
						: left.WithoutTrailingTrivia());
				return Chain.Create(Replace(node, newNode.AnnotateReformatAndSelect()));
			}
		}

		sealed class MultiLineExpressionRefactoring : ReplaceNode
		{
			string _Title;
			public override int IconId => IconIds.MultiLine;
			public override string Title => _Title;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				var nodeKind = node.Kind();
				switch (nodeKind) {
					case SyntaxKind.LogicalAndExpression: _Title = R.CMD_MultiLineLogicalAnd; break;
					case SyntaxKind.AddExpression:
					case SyntaxKind.SubtractExpression: _Title = R.CMD_MultiLineOperands; break;
					case SyntaxKind.LogicalOrExpression: _Title = R.CMD_MultiLineLogicalOr; break;
					case SyntaxKind.CoalesceExpression: _Title = R.CMD_MultiLineCoalesce; break;
					case SyntaxKind.ConditionalExpression:
						_Title = R.CMD_MultiLineConditional;
						return node.IsMultiLine(false) == false;
					default: return false;
				}
				SyntaxNode p = node.Parent;
				if (nodeKind.IsAny(SyntaxKind.AddExpression, SyntaxKind.SubtractExpression)) {
					while (p.Kind().IsAny(SyntaxKind.AddExpression, SyntaxKind.SubtractExpression)) {
						node = p;
						p = p.Parent;
					}
				}
				else {
					while (p.IsKind(nodeKind)) {
						node = p;
						p = p.Parent;
					}
				}
				return node.IsMultiLine(false) == false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				var nodeKind = node.Kind();
				var (indent, newLine) = ctx.GetIndentAndNewLine(node.SpanStart);
				BinaryExpressionSyntax newExp = null;
				SyntaxToken token;
				if (nodeKind == SyntaxKind.LogicalAndExpression) {
					ReformatLogicalExpressions(ref node, ref newExp, newLine, indent, nodeKind);
				}
				else if (nodeKind.IsAny(SyntaxKind.AddExpression, SyntaxKind.SubtractExpression)) {
					ReformatAddExpressions(ref node, ref newExp, newLine, indent, nodeKind);
				}
				else if (nodeKind == SyntaxKind.LogicalOrExpression) {
					ReformatLogicalExpressions(ref node, ref newExp, newLine, indent, nodeKind);
				}
				else if (nodeKind == SyntaxKind.CoalesceExpression) {
					token = CreateTokenWithTrivia(indent, SyntaxKind.QuestionQuestionToken);
					ReformatCoalesceExpression(ref node, ref newExp, newLine, token, nodeKind);
				}
				else if (nodeKind == SyntaxKind.ConditionalExpression) {
					return Chain.Create(ReformatConditionalExpression((ConditionalExpressionSyntax)node, indent, newLine));
				}
				else {
					return Enumerable.Empty<RefactoringAction>();
				}
				return Chain.Create(Replace(node, newExp.AnnotateSelect()));
			}

			static SyntaxToken CreateTokenWithTrivia(SyntaxTriviaList indent, SyntaxKind syntaxKind) {
				return SF.Token(syntaxKind).WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space);
			}

			static void ReformatAddExpressions(ref SyntaxNode node, ref BinaryExpressionSyntax newExp, SyntaxTrivia newLine, SyntaxTriviaList indent, SyntaxKind nodeKind) {
				var exp = (BinaryExpressionSyntax)node;
				while (exp.Left.Kind().IsAny(SyntaxKind.AddExpression, SyntaxKind.SubtractExpression)) {
					exp = (BinaryExpressionSyntax)exp.Left;
				}
				do {
					node = exp;
					newExp = exp.Update(((ExpressionSyntax)newExp ?? exp.Left).WithTrailingTrivia(newLine),
						exp.OperatorToken.WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
						exp.Right);
					exp = exp.Parent as BinaryExpressionSyntax;
				} while (exp != null && exp.Kind().IsAny(SyntaxKind.AddExpression, SyntaxKind.SubtractExpression));
			}

			static void ReformatLogicalExpressions(ref SyntaxNode node, ref BinaryExpressionSyntax newExp, SyntaxTrivia newLine, SyntaxTriviaList indent, SyntaxKind nodeKind) {
				var exp = (BinaryExpressionSyntax)node;
				while (exp.Left.IsKind(nodeKind)) {
					exp = (BinaryExpressionSyntax)exp.Left;
				}
				do {
					node = exp;
					newExp = exp.Update(((ExpressionSyntax)newExp ?? exp.Left).WithTrailingTrivia(newLine),
						exp.OperatorToken.WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
						exp.Right);
					exp = exp.Parent as BinaryExpressionSyntax;
				} while (exp != null && exp.Left.IsKind(nodeKind));
			}

			static void ReformatCoalesceExpression(ref SyntaxNode node, ref BinaryExpressionSyntax newExp, SyntaxTrivia newLine, SyntaxToken token, SyntaxKind nodeKind) {
				var exp = (BinaryExpressionSyntax)node;
				while (exp.Right.IsKind(nodeKind)) {
					exp = (BinaryExpressionSyntax)exp.Right;
				}
				do {
					node = exp;
					newExp = exp.Update(exp.Left.WithTrailingTrivia(newLine),
						token,
						(ExpressionSyntax)newExp ?? exp.Right);
					exp = exp.Parent as BinaryExpressionSyntax;
				} while (exp != null);
			}

			static RefactoringAction ReformatConditionalExpression(ConditionalExpressionSyntax node, SyntaxTriviaList indent, SyntaxTrivia newLine) {
				var newNode = node.Update(node.Condition.WithTrailingTrivia(newLine),
					node.QuestionToken.WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
					node.WhenTrue.WithTrailingTrivia(newLine),
					node.ColonToken.WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
					node.WhenFalse);
				return Replace(node, newNode.AnnotateSelect());
			}
		}

		sealed class MultiLineListRefactoring : ReplaceNode
		{
			string _Title;
			public override int IconId => IconIds.MultiLineList;
			public override string Title => _Title;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.Node;
				switch (node.Kind()) {
					case SyntaxKind.ArgumentList:
						if (((ArgumentListSyntax)node).Arguments.Count > 1 && node.IsMultiLine(false) == false) {
							_Title = R.CMD_ArgumentsOnMultiLine;
							return true;
						}
						break;
					case SyntaxKind.ParameterList:
						if (((ParameterListSyntax)node).Parameters.Count > 1 && node.IsMultiLine(false) == false) {
							_Title = R.CMD_MultiLineParameters;
							return true;
						}
						break;
					case SyntaxKind.ArrayInitializerExpression:
					case SyntaxKind.CollectionInitializerExpression:
					case SyntaxKind.ObjectInitializerExpression:
						if (((InitializerExpressionSyntax)node).Expressions.Count > 1 && node.IsMultiLine(false) == false) {
							_Title = R.CMD_MultiLineExpressions;
							return true;
						}
						break;
					case SyntaxKind.VariableDeclaration:
						if (((VariableDeclarationSyntax)node).Variables.Count > 1 && node.IsMultiLine(false) == false) {
							_Title = R.CMD_MultiLineDeclarations;
							return true;
						}
						break;
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var node = ctx.Node;
				SyntaxNode newNode = null;
				if (node is ArgumentListSyntax al) {
					newNode = al.WithArguments(MakeMultiLine(al.Arguments, ctx));
				}
				else if (node is ParameterListSyntax pl) {
					newNode = pl.WithParameters(MakeMultiLine(pl.Parameters, ctx));
				}
				else if (node is InitializerExpressionSyntax ie) {
					newNode = MakeMultiLine(ie, ctx);
				}
				else if (node is VariableDeclarationSyntax va) {
					newNode = va.WithVariables(MakeMultiLine(va.Variables, ctx));
				}
				if (newNode != null) {
					return Chain.Create(Replace(node, newNode.AnnotateSelect()));
				}
				return Enumerable.Empty<RefactoringAction>();
			}

			static SeparatedSyntaxList<T> MakeMultiLine<T>(SeparatedSyntaxList<T> list, RefactoringContext ctx) where T : SyntaxNode {
				var (indent, newLine) = ctx.GetIndentAndNewLine(ctx.Node.SpanStart);
				var l = new T[list.Count];
				for (int i = 0; i < l.Length; i++) {
					l[i] = i > 0 ? list[i].WithLeadingTrivia(indent) : list[i];
				}
				return SF.SeparatedList(l,
					Enumerable.Repeat(SF.Token(SyntaxKind.CommaToken).WithTrailingTrivia(newLine), l.Length - 1));
			}

			static InitializerExpressionSyntax MakeMultiLine(InitializerExpressionSyntax initializer, RefactoringContext ctx) {
				var (indent, newLine) = ctx.GetIndentAndNewLine(ctx.Node.SpanStart, 0);
				var indent2 = indent.Add(SF.Whitespace(ctx.WorkspaceOptions.GetIndentString()));
				var list = initializer.Expressions;
				var l = new ExpressionSyntax[list.Count];
				for (int i = 0; i < l.Length; i++) {
					l[i] = list[i].WithLeadingTrivia(indent2);
				}
				l[l.Length - 1] = l[l.Length - 1].WithTrailingTrivia(newLine);
				return initializer.Update(initializer.OpenBraceToken.WithTrailingTrivia(newLine),
					SF.SeparatedList(l, Enumerable.Repeat(SF.Token(SyntaxKind.CommaToken).WithTrailingTrivia(newLine), l.Length - 1)),
					initializer.CloseBraceToken.WithLeadingTrivia(indent)
				);
			}
		}

		sealed class MultiLineMemberAccessRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.MultiLineList;
			public override string Title => R.CMD_MultiLineMemberAccess;

			public override bool Accept(RefactoringContext ctx) {
				return ctx.NodeIncludeTrivia.Kind().IsAny(SyntaxKind.SimpleMemberAccessExpression, SyntaxKind.ConditionalAccessExpression);
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				var (indent, newLine) = ctx.GetIndentAndNewLine(node.SpanStart);
				ExpressionSyntax newExp = null;
				while (true) {
					if (node is MemberAccessExpressionSyntax ma) {
						newExp = ma.Update((newExp ?? ma.Expression).WithTrailingTrivia(newLine), ma.OperatorToken.WithLeadingTrivia(indent), ma.Name);
					}
					else {
						if (node is ConditionalAccessExpressionSyntax ca) {
							if (ca.WhenNotNull.FullSpan.Contains(ctx.Token.FullSpan.Start)) {
								newExp = (ExpressionSyntax)ca.Update(ca.Expression, ca.OperatorToken, (newExp ?? ca.WhenNotNull));
							}
							else {
								newExp = (ExpressionSyntax)ca.Update((newExp ?? ca.Expression).WithTrailingTrivia(newLine), ca.OperatorToken.WithLeadingTrivia(indent), WrapAccess(ca.WhenNotNull, indent, newLine));
							}
						}
						else {
							break;
						}
					}

					if (node.Parent.Kind().IsAny(SyntaxKind.SimpleMemberAccessExpression, SyntaxKind.ConditionalAccessExpression)) {
						node = node.Parent;
					}
					else if (node.Parent is InvocationExpressionSyntax i
						&& i.Parent.Kind().IsAny(SyntaxKind.SimpleMemberAccessExpression, SyntaxKind.ConditionalAccessExpression)) {
						newExp = i.Update(newExp, i.ArgumentList);
						node = i.Parent;
					}
					else {
						break;
					}
				}
				return Chain.Create(Replace(node, newExp.AnnotateSelect()));
			}

			static ExpressionSyntax WrapAccess(ExpressionSyntax expression, SyntaxTriviaList indent, SyntaxTrivia newLine) {
				if (expression is MemberAccessExpressionSyntax ma) {
					return ma.Update(WrapAccess(ma.Expression, indent, newLine).WithTrailingTrivia(newLine),
						ma.OperatorToken.WithLeadingTrivia(indent),
						ma.Name);
				}
				else if (expression is InvocationExpressionSyntax i) {
					return i.Update(WrapAccess(i.Expression, indent, newLine), i.ArgumentList);
				}
				else if (expression is ConditionalAccessExpressionSyntax ca) {
					return ca.Update(ca.Expression.WithTrailingTrivia(newLine), ca.OperatorToken.WithLeadingTrivia(indent), WrapAccess(ca.WhenNotNull, indent, newLine));
				}
				else {
					return expression;
				}
			}
		}

		sealed class ConcatToInterpolatedStringRefactoring : ReplaceNode
		{
			const int CONCAT = 1, ADD = 2;
			const char StartChar = '{', EndChar = '}';
			const string Start = "{", End = "}", StartSubstitution = "{{", EndSubstitution = "}}";

			static readonly char[] StartEndChars = new[] { StartChar, EndChar };
			int _Mode;

			public override int IconId => IconIds.InterpolatedString;
			public override string Title => R.CMD_ConcatToInterpolatedString;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.Node;
				InvocationExpressionSyntax ie;
				if (node is IdentifierNameSyntax name) {
					if (name.GetName() == nameof(String.Concat)
						&& (ie = node.FirstAncestorOrSelf<InvocationExpressionSyntax>()) != null
						&& ie.ArgumentList.Arguments.Count > 1
						&& ie.Expression.GetLastIdentifier() == name) {
						_Mode = 1;
						return true;
					}
				}
				else if (node.IsKind(SyntaxKind.AddExpression)
					&& ctx.SemanticContext.SemanticModel.GetTypeInfo(node).ConvertedType.SpecialType == SpecialType.System_String) {
					_Mode = 2;
					return true;
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				if (_Mode == CONCAT) {
					var ie = ctx.Node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
					if (ie == null) {
						return Enumerable.Empty<RefactoringAction>();
					}
					return Chain.Create(Replace(ie,
						SF.InterpolatedStringExpression(
							SF.Token(SyntaxKind.InterpolatedStringStartToken),
							new SyntaxList<InterpolatedStringContentSyntax>(ArgumentsToInterpolatedStringContents(ie.ArgumentList.Arguments))
							).WithTrailingTrivia(ie.GetTrailingTrivia()).AnnotateSelect()
						));
				}
				if (_Mode == ADD) {
					var node = ctx.Node;
					while (node.Parent.IsKind(SyntaxKind.AddExpression)) {
						node = node.Parent;
					}
					var add = (BinaryExpressionSyntax)node;
					if (add.Parent.IsKind(SyntaxKind.ParenthesizedExpression)) {
						node = add.Parent;
					}
					return Chain.Create(Replace(node, SF.InterpolatedStringExpression(
							SF.Token(SyntaxKind.InterpolatedStringStartToken),
							new SyntaxList<InterpolatedStringContentSyntax>(AddExpressionsToInterpolatedStringContents(add))
							).NormalizeWhitespace().WithTriviaFrom(node).AnnotateSelect()
						));
				}
				return Enumerable.Empty<RefactoringAction>();
			}

			static IEnumerable<InterpolatedStringContentSyntax> ArgumentsToInterpolatedStringContents(SeparatedSyntaxList<ArgumentSyntax> arguments) {
				return ExpressionsToInterpolatedStringContents(arguments.Select(a => a.Expression));
			}

			static IEnumerable<InterpolatedStringContentSyntax> AddExpressionsToInterpolatedStringContents(BinaryExpressionSyntax add) {
				while (add.Left is BinaryExpressionSyntax bin && bin.IsKind(SyntaxKind.AddExpression)) {
					add = bin;
				}
				return ExpressionsToInterpolatedStringContents(GetBinaryExpressionOperands(add));
			}

			static IEnumerable<InterpolatedStringContentSyntax> ExpressionsToInterpolatedStringContents(IEnumerable<ExpressionSyntax> expressions) {
				var ch = new Chain<InterpolatedStringContentSyntax>();
				foreach (var exp in expressions) {
					switch (exp.Kind()) {
						case SyntaxKind.StringLiteralExpression:
							var s = (LiteralExpressionSyntax)exp;
							var t = s.Token.Text;
							switch (s.Token.Kind()) {
								case SyntaxKind.StringLiteralToken:
									t = t[0] == '@'
										? t.Substring(2, t.Length - 3).Replace("\"\"", "\\\"")
										: t.Substring(1, t.Length - 2);
									t = ReplaceBraces(t);
									break;
								case SyntaxKind.InterpolatedStringToken:
								case CodeAnalysisHelper.SingleLineRawStringLiteralToken:
								case CodeAnalysisHelper.MultiLineRawStringLiteralToken:
								default:
									ch.AddOrInit((InterpolatedStringContentSyntax)SF.Interpolation(exp.WithoutTrivia()));
									goto NEXT;
							}
							ch.AddOrInit(SF.InterpolatedStringText(SF.Token(default, SyntaxKind.InterpolatedStringTextToken, t, s.Token.ValueText, default)));
						NEXT:
							break;
						case SyntaxKind.InterpolatedStringExpression:
							var ise = (InterpolatedStringExpressionSyntax)exp;
							if (ise.StringStartToken.IsKind(SyntaxKind.InterpolatedVerbatimStringStartToken)) {
								foreach (var item in ise.Contents) {
									if (item is InterpolatedStringTextSyntax ist
										&& (t = ist.TextToken.Text).Contains("\"\"")) {
										t = ReplaceBraces(t.Replace("\"\"", "\\\""));
										ch.AddOrInit(SF.InterpolatedStringText(SF.Token(default, SyntaxKind.InterpolatedStringTextToken, t, ist.TextToken.ValueText, default)));
									}
									else {
										ch.AddOrInit(item);
									}
								}
							}
							else {
								foreach (var item in ise.Contents) {
									ch.AddOrInit(item);
								}
							}
							break;
						case SyntaxKind.CharacterLiteralExpression:
							var c = (LiteralExpressionSyntax)exp;
							t = c.Token.Text;
							t = t.Substring(1, t.Length - 2);
							switch (t) {
								case Start: t = StartSubstitution; break;
								case End: t = EndSubstitution; break;
							}
							ch.AddOrInit(SF.InterpolatedStringText(SF.Token(default, SyntaxKind.InterpolatedStringTextToken, t, c.Token.ValueText, default)));
							break;
						case SyntaxKind.NullLiteralExpression:
							ch.AddOrInit(SF.InterpolatedStringText());
							break;
						case SyntaxKind.ConditionalExpression:
							ch.AddOrInit(SF.Interpolation(SF.ParenthesizedExpression(exp.WithoutTrivia())));
							break;
						default:
							ch.AddOrInit(SF.Interpolation(exp.WithoutTrivia()));
							break;
					}
				}
				return ch;
			}

			private static string ReplaceBraces(string t) {
				return t.IndexOfAny(StartEndChars) >= 0
					? t.Replace(Start, StartSubstitution).Replace(End, EndSubstitution)
					: t;
			}

			static IEnumerable<ExpressionSyntax> GetBinaryExpressionOperands(BinaryExpressionSyntax bin) {
				var kind = bin.Kind();
				var ch = Chain.Create(bin.Left);
				do {
					ch.Add(bin.Right);
				}
				while ((bin = bin.Parent as BinaryExpressionSyntax) != null && bin.IsKind(kind));
				return ch;
			}
		}
	}
}

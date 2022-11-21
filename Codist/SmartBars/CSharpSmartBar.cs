﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using R = Codist.Properties.Resources;

namespace Codist.SmartBars
{
	//todo Make commands async and cancellable
	/// <summary>
	/// An extended <see cref="SmartBar"/> for C# content type.
	/// </summary>
	sealed class CSharpSmartBar : SmartBar
	{
		static readonly Taggers.HighlightClassifications __HighlightClassifications = Taggers.HighlightClassifications.Instance;
		static readonly Refactorings.IRefactoring[] __Refactorings = new Refactorings.IRefactoring[] {
			Refactorings.ReplaceToken.InvertOperator,
			Refactorings.ReplaceNode.MergeToConditional,
			Refactorings.ReplaceNode.WrapInElse,
			Refactorings.ReplaceNode.WrapInIf,
			Refactorings.ReplaceNode.WrapInTryCatch,
			Refactorings.ReplaceNode.MultiLineConditional,
			Refactorings.ReplaceNode.ChangeConditionalToIf,
			Refactorings.ReplaceNode.ChangeIfToConditional,
			Refactorings.ReplaceNode.MergeCondition,
			Refactorings.ReplaceNode.SwapOperands,
			Refactorings.ReplaceNode.NestCondition,
			Refactorings.ReplaceNode.AddBraces,
			Refactorings.ReplaceNode.DeleteCondition,
			Refactorings.ReplaceNode.RemoveContainingStatement
		};
		static readonly string[] __UnitTestingNamespace = new[] { "UnitTesting", "TestTools", "VisualStudio", "Microsoft" };
		SemanticContext _Context;
		ExternalAdornment _SymbolListContainer;
		ISymbol _Symbol;

		public CSharpSmartBar(IWpfTextView view, Microsoft.VisualStudio.Text.Operations.ITextSearchService2 textSearchService) : base(view, textSearchService) {
			ThreadHelper.ThrowIfNotOnUIThread();
			_Context = SemanticContext.GetOrCreateSingetonInstance(view);
			_SymbolListContainer = ExternalAdornment.GetOrCreate(view);
			view.Closed += View_Closed;
		}

		ToolBar MyToolBar => ToolBar2;

		protected override void AddCommands(CancellationToken cancellationToken) {
			if (UpdateSemanticModel() && _Context.NodeIncludeTrivia != null) {
				AddContextualCommands(cancellationToken);
			}
			base.AddCommands(cancellationToken);
		}

		static CommandItem CreateCommandMenu(int imageId, string title, string emptyMenuTitle, Action<CommandContext, MenuItem> itemPopulator) {
			return new CommandItem(imageId, title, ctrl => ctrl.StaysOpenOnClick = true, ctx => {
				var menuItem = ctx.Sender as ThemedMenuItem;
				if (menuItem.Items.Count > 0 || menuItem.SubMenuHeader != null) {
					return;
				}
				ctx.KeepToolBarOnClick = true;
				itemPopulator(ctx, menuItem);
				if (menuItem.Items.Count == 0) {
					menuItem.Items.Add(new ThemedMenuItem { Header = emptyMenuTitle, IsEnabled = false });
				}
				else {
					CreateItemsFilter(menuItem);
				}
				menuItem.IsSubmenuOpen = true;
			});
		}

		static void CreateItemsFilter(ThemedMenuItem menuItem) {
			menuItem.SubMenuHeader = new StackPanel {
				Margin = WpfHelper.TopItemMargin,
				Children = {
					new SymbolFilterBox(new MenuItemFilter(menuItem.Items)),
					new Separator()
				}
			};
		}

		void AddContextualCommands(CancellationToken cancellationToken) {
			var isReadOnly = _Context.View.IsCaretInReadOnlyRegion();
			var node = _Context.NodeIncludeTrivia;
			var nodeKind = node.Kind();
			if (isReadOnly == false && nodeKind == SyntaxKind.XmlText) {
				AddXmlDocCommands();
				return;
			}
			var trivia = _Context.GetNodeTrivia();
			if (trivia.RawKind == 0) {
				var token = _Context.Token;
				var tokenKind = token.Kind();
				if (token.Span.Contains(View.Selection, true)) {
					switch (tokenKind) {
						case SyntaxKind.IdentifierToken:
						case SyntaxKind.SetKeyword:
						case SyntaxKind.GetKeyword:
						case CodeAnalysisHelper.InitKeyword:
							if (nodeKind.IsDeclaration()
								|| nodeKind == SyntaxKind.PredefinedType
								|| node is TypeSyntax
								|| nodeKind == SyntaxKind.Parameter
								|| nodeKind == SyntaxKind.VariableDeclarator
								|| nodeKind == SyntaxKind.ForEachStatement
								|| nodeKind == SyntaxKind.SingleVariableDesignation
								|| node is AccessorDeclarationSyntax) {
								// selection is within a symbol
								_Symbol = SyncHelper.RunSync(() => _Context.GetSymbolAsync(cancellationToken));
								if (_Symbol != null) {
									AddSymbolCommands(nodeKind);
								}

								if (isReadOnly == false) {
									if (_Symbol?.IsPublicConcreteInstance() == true) {
										if (nodeKind == SyntaxKind.MethodDeclaration) {
											if (_Symbol.GetContainingTypes().All(t => t.IsPublicConcreteInstance()) && _Symbol.GetAttributes().Any(a => a.AttributeClass.MatchTypeName("TestMethodAttribute", __UnitTestingNamespace))) {
												AddEditorCommand(MyToolBar, IconIds.DebugTest, "TestExplorer.DebugAllTestsInContext", R.CMD_DebugTestMethod, "TestExplorer.RunAllTestsInContext");
											}
										}
										else if (nodeKind == SyntaxKind.ClassDeclaration) {
											if (_Symbol.GetAttributes().Any(a => a.AttributeClass.MatchTypeName("TestClassAttribute", __UnitTestingNamespace))) {
												AddEditorCommand(MyToolBar, IconIds.DebugTest, "TestExplorer.DebugAllTestsInContext", R.CMD_DebugTestClass, "TestExplorer.RunAllTestsInContext");
											}
										}
									}
									AddRefactorCommands(node);
								}
							}
							else if (nodeKind == SyntaxKind.TypeParameter) {
								_Symbol = SyncHelper.RunSync(() => _Context.GetSymbolAsync(cancellationToken));
								if (_Symbol != null && isReadOnly == false) {
									AddRenameCommand();
								}
							}
							if (_Symbol != null) {
								if (isReadOnly == false) {
									AddCommand(MyToolBar, IconIds.SelectAll, R.CMD_SelectSymbolInDocument, SelectSymbolOccurrences);
								}
								if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight)
									&& Taggers.SymbolMarkManager.CanBookmark(_Symbol)) {
									AddCommands(MyToolBar, IconIds.Marks, R.CMD_MarkSymbol, null, GetMarkerCommands);
								}
							}
							break;
						default:
							if (tokenKind.IsPredefinedSystemType()) {
								goto case SyntaxKind.IdentifierToken;
							}
							if (View.Selection.StreamSelectionSpan.Length < 4
								&& (tokenKind >= SyntaxKind.TildeToken && tokenKind <= SyntaxKind.PercentEqualsToken
									|| tokenKind == SyntaxKind.IsKeyword
									|| tokenKind == SyntaxKind.AsKeyword)
								&& SelectionIs<SyntaxNode>()) {
								AddCommand(MyToolBar, IconIds.SelectBlock, R.CMD_SelectBlock, SelectNodeAsKind<SyntaxNode>);
							}
							else if (tokenKind >= SyntaxKind.NumericLiteralToken && tokenKind <= SyntaxKind.StringLiteralToken) {
								AddEditorCommand(MyToolBar, IconIds.FindReference, "Edit.FindAllReferences", R.CMD_FindAllReferences);
							}
							break;
					}
				}
				else if (nodeKind.IsRegionalDirective()) {
					AddDirectiveCommands();
				}
				if (isReadOnly == false) {
					if (tokenKind == SyntaxKind.TrueKeyword || tokenKind == SyntaxKind.FalseKeyword) {
						AddCommand(MyToolBar, IconIds.ToggleValue, R.CMD_ToggleValue, ctx => Replace(ctx, v => v == "true" ? "false" : "true", true));
					}
					else if (tokenKind == SyntaxKind.ExplicitKeyword || tokenKind == SyntaxKind.ImplicitKeyword) {
						AddCommand(MyToolBar, IconIds.ToggleValue, R.CMD_ToggleOperator, ctx => Replace(ctx, v => v == "implicit" ? "explicit" : "implicit", true));
					}
					if (nodeKind == SyntaxKind.VariableDeclarator) {
						if (node?.Parent?.Parent is MemberDeclarationSyntax) {
							AddCommand(MyToolBar, IconIds.AddXmlDoc, R.CMD_InsertComment, ctx => {
								TextEditorHelper.ExecuteEditorCommand("Edit.InsertComment");
								ctx.View.Selection.Clear();
							});
						}
					}
					else if (nodeKind.IsDeclaration()) {
						if (node is TypeDeclarationSyntax || node is MemberDeclarationSyntax || node is ParameterListSyntax) {
							AddCommand(MyToolBar, IconIds.AddXmlDoc, R.CMD_InsertComment, ctx => {
								TextEditorHelper.ExecuteEditorCommand("Edit.InsertComment");
								ctx.View.Selection.Clear();
							});
						}
					}
				}
			}
			if (isReadOnly == false) {
				var refactoringContext = new Refactorings.RefactoringContext(_Context);
				if (refactoringContext.SelectedStatementInfo.Statements != null) {
					AddEditorCommand(MyToolBar, IconIds.ExtractMethod, "Refactor.ExtractMethod", R.CMD_ExtractMethod);
				}
				if (HasRefactoring(__Refactorings, refactoringContext)) {
					AddCommand(MyToolBar, IconIds.Refactoring, R.CMD_RefactorSelection, ShowRefactorMenu);
				}
				AddCommentCommands();
				AddEditorCommand(MyToolBar, IconIds.QuickAction, "View.QuickActionsForPosition", R.CMD_QuickAction);
			}
		}

		static bool HasRefactoring(Refactorings.IRefactoring[] refactorings, Refactorings.RefactoringContext context) {
			foreach (var item in refactorings) {
				if (item.Accept(context)) {
					return true;
				}
			}
			return false;
		}

		void ShowRefactorMenu(CommandContext ctx) {
			ctx.KeepToolBar(false);
			if (UpdateSemanticModel() == false) {
				return;
			}
			var m = new ContextMenu() {
				Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
				PlacementTarget = ctx.Sender,
				Resources = SharedDictionaryManager.ContextMenu
			};
			var rc = new Refactorings.RefactoringContext(_Context);
			AddRefactoringCommands(m, __Refactorings, rc);
			ctx.Sender.ContextMenu = m;
			m.Closed += Menu_Closed;
			m.IsOpen = true;
		}

		void AddRefactoringCommands(ContextMenu menu, Refactorings.IRefactoring[] refactorings, Refactorings.RefactoringContext ctx) {
			foreach (var item in refactorings) {
				if (item.Accept(ctx)) {
					menu.Items.Add(new CommandMenuItem(this, new CommandItem(item.IconId, item.Title, c => item.Refactor(((CSharpSmartBar)c.Bar)._Context))));
				}
			}
		}

		void AddSymbolCommands(SyntaxKind nodeKind) {
			if (nodeKind == SyntaxKind.IdentifierName || nodeKind == SyntaxKind.GenericName) {
				AddEditorCommand(MyToolBar, IconIds.GoToDefinition, "Edit.GoToDefinition", R.CMD_GoToDefinitionPeek, "Edit.PeekDefinition");
			}
			AddCommand(MyToolBar, IconIds.SymbolAnalysis, R.CMD_AnalyzeSymbol, ShowSymbolContextMenu);
		}

		void SelectSymbolOccurrences(CommandContext ctx) {
			ctx.KeepToolBar(false);
			if (UpdateSemanticModel() == false) {
				return;
			}
			var selections = ctx.View.GetMultiSelectionBroker();
			var symbol = _Symbol;
			SelectSymbolDefinitionAndReferences(selections, symbol);
			if (symbol.Kind == SymbolKind.Method
				&& symbol is IMethodSymbol m && m.MethodKind == MethodKind.Constructor) {
				SelectSymbolDefinitionAndReferences(selections, symbol.ContainingType);
			}
		}

		void SelectSymbolDefinitionAndReferences(IMultiSelectionBroker selections, ISymbol symbol) {
			var snapshot = selections.CurrentSnapshot;
			foreach (var refs in SyncHelper.RunSync(() => Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindReferencesAsync(symbol, _Context.Document.Project.Solution, System.Collections.Immutable.ImmutableHashSet.Create(_Context.Document), default))) {
				selections.AddSelections(refs.Locations.Select(l => l.Location.SourceSpan));
			}
			foreach (var sr in symbol.Locations) {
				if (sr.IsInSource && sr.SourceTree == _Context.Compilation.SyntaxTree) {
					selections.AddSelection(sr.SourceSpan);
				}
			}
		}

		void ShowSymbolContextMenu(CommandContext ctx) {
			ctx.KeepToolBar(false);
			if (UpdateSemanticModel() == false) {
				return;
			}
			var m = new CSharpSymbolContextMenu(_Symbol, _Context.NodeIncludeTrivia, _Context) {
				Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
				PlacementTarget = ctx.Sender
			};
			m.AddAnalysisCommands();
			m.AddFindAllReferencesCommand();
			m.AddGoToAnyCommands();
			ctx.Sender.ContextMenu = m;
			m.Closed += Menu_Closed;
			m.IsOpen = true;
		}

		void AddDirectiveCommands() {
			AddCommand(MyToolBar, IconIds.SelectCode, R.CMD_SelectDirectiveRegion, SelectDirectiveRegion);
		}

		void SelectDirectiveRegion(CommandContext ctx) {
			var a = _Context.NodeIncludeTrivia as DirectiveTriviaSyntax ?? default;
			if (a == null) {
				return;
			}
			DirectiveTriviaSyntax b;
			if (a.IsKind(SyntaxKind.EndRegionDirectiveTrivia) || a.IsKind(SyntaxKind.EndIfDirectiveTrivia)) {
				b = a;
				a = b.GetPreviousDirective();
				if (a == null) {
					return;
				}
			}
			else {
				b = a.GetNextDirective();
			}
			ctx.View.SelectSpan(new SnapshotSpan(ctx.View.TextSnapshot, Span.FromBounds(a.FullSpan.Start, b.FullSpan.End)));
		}

		bool SelectionIs<TNode>() where TNode : SyntaxNode {
			var s = View.Selection.SelectedSpans.FirstOrDefault().ToTextSpan();
			foreach (var item in _Context.NodeIncludeTrivia.AncestorsAndSelf()) {
				if (item is TNode && item.Span.Contains(s) && item.Span != s) {
					return true;
				}
			}
			return false;
		}

		void SelectNodeAsKind<TNode>(CommandContext ctx) where TNode : SyntaxNode {
			var s = View.Selection.SelectedSpans.FirstOrDefault().ToTextSpan();
			foreach (var item in _Context.NodeIncludeTrivia.AncestorsAndSelf()) {
				if (item is TNode && item.Span.Contains(s) && item.Span != s) {
					ctx.KeepToolBar(false);
					item.SelectNode(false);
					ctx.KeepToolBar(true);
					return;
				}
			}
		}

		void AddCommentCommands() {
			AddCommand(MyToolBar, IconIds.Comment, R.CMD_CommentSelection, ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				TextEditorHelper.ExecuteEditorCommand("Edit.CommentSelection");
			});
			if (View.TryGetFirstSelectionSpan(out var ss) && ss.Length < 0x2000) {
				foreach (var t in _Context.Compilation.DescendantTrivia(ss.ToTextSpan())) {
					if (t.IsKind(SyntaxKind.SingleLineCommentTrivia)) {
						AddEditorCommand(MyToolBar, IconIds.Uncomment, "Edit.UncommentSelection", R.CMD_UncommentSelection);
						return;
					}
				}
			}
			if (_Context.GetLineComment().RawKind != 0) {
				AddEditorCommand(MyToolBar, IconIds.Uncomment, "Edit.UncommentSelection", R.CMD_UncommentSelection);
			}
		}

		void AddRenameCommand() {
			if (_Symbol != null && _Symbol.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
				AddCommand(MyToolBar, IconIds.Rename, R.CMD_RenameSymbol, ctx => {
					ctx.KeepToolBar(false);
					TextEditorHelper.ExecuteEditorCommand("Refactor.Rename");
				});
			}
		}

		void AddRefactorCommands(SyntaxNode node) {
			AddRenameCommand();
			switch (node.Kind()) {
				case SyntaxKind.Parameter:
					if (node.Parent.IsKind(SyntaxKind.ParameterList)) {
						AddEditorCommand(MyToolBar, IconIds.ReorderParameters, "Refactor.ReorderParameters", R.CMD_ReorderParameters);
					}
					break;
				case SyntaxKind.IdentifierName:
				case SyntaxKind.PredefinedType:
					if (node.Parent.IsKind(SyntaxKind.Parameter) && node.Parent.Parent.IsKind(SyntaxKind.ParameterList)) {
						AddEditorCommand(MyToolBar, IconIds.ReorderParameters, "Refactor.ReorderParameters", R.CMD_ReorderParameters);
					}
					break;
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.StructDeclaration:
				case CodeAnalysisHelper.RecordDeclaration:
				case CodeAnalysisHelper.RecordStructDesclaration:
					if ((node as TypeDeclarationSyntax).Modifiers.Any(SyntaxKind.StaticKeyword) == false) {
						AddEditorCommand(MyToolBar, IconIds.ExtractInterface, "Refactor.ExtractInterface", R.CMD_ExtractInterface);
					}
					break;
				case SyntaxKind.MethodDeclaration:
					AddCommand(MyToolBar, IconIds.DeleteMethod, R.CMD_DeleteMethod, ctx => {
						ctx.View.SelectNode(node, true);
						ctx.View.Edit(node, (view, n, edit) => {
							edit.Delete(n.FullSpan.ToSpan());
						});
					});
					break;
				case SyntaxKind.VariableDeclarator:
					if (node.Parent?.Parent.IsKind(SyntaxKind.FieldDeclaration) == true) {
						AddEditorCommand(MyToolBar, IconIds.EncapsulateField, "Refactor.EncapsulateField", R.CMD_EncapsulateField);
					}
					break;
			}
		}

		void AddXmlDocCommands() {
			AddCommand(MyToolBar, IconIds.TagCode, R.CMD_TagXmlDocC, ctx => WrapWith(ctx, "<c>", "</c>", true));
			AddCommand(MyToolBar, IconIds.TagXmlDocSee, R.CMD_TagXmlDocSee, WrapXmlDocSee);
			AddCommand(MyToolBar, IconIds.TagXmlDocPara, R.CMD_TagXmlDocPara, ctx => WrapWith(ctx, "<para>", "</para>", false));
			AddCommand(MyToolBar, IconIds.TagBold, R.CMD_TagXmlDocB, ctx => WrapWith(ctx, "<b>", "</b>", true));
			AddCommand(MyToolBar, IconIds.TagItalic, R.CMD_TagXmlDocI, ctx => WrapWith(ctx, "<i>", "</i>", true));
			AddCommand(MyToolBar, IconIds.TagUnderline, R.CMD_TagXmlDocU, ctx => WrapWith(ctx, "<u>", "</u>", true));
			AddCommand(MyToolBar, IconIds.TagHyperLink, R.CMD_TagXmlDocA, MakeUrl);
			AddCommand(MyToolBar, IconIds.Comment, R.CMD_CommentSelection, ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				TextEditorHelper.ExecuteEditorCommand("Edit.CommentSelection");
			});
		}

		void WrapXmlDocSee(CommandContext ctx) {
			// updates the semantic model before executing the command,
			// for it could be modified by external editor commands or duplicated document windows
			if (UpdateSemanticModel() == false) {
				return;
			}
			ctx.View.Edit(new { me = this, ctx }, (view, arg, edit) => {
				arg.ctx.KeepToolBar(true);
				string t = null;
				foreach (var item in view.Selection.SelectedSpans) {
					t = item.GetText();
					var d = arg.me._Context.GetNode(item.Start, false, false).GetAncestorOrSelfDeclaration();
					if (d != null) {
						if (((d as BaseMethodDeclarationSyntax)?.ParameterList
							?? (d as DelegateDeclarationSyntax)?.ParameterList)
								?.Parameters.Any(p => p.Identifier.Text == t) == true) {
							edit.Replace(item, $"<paramref name=\"{t}\"/>");
							continue;
						}
						if (d.FindTypeParameter(t) != null) {
							edit.Replace(item, $"<typeparamref name=\"{t}\"/>");
							continue;
						}
					}
					edit.Replace(item, (SyntaxFacts.GetKeywordKind(t) != SyntaxKind.None ? "<see langword=\"" : "<see cref=\"") + t + "\"/>");
				}
				if (t != null && Keyboard.Modifiers.MatchFlags(ModifierKeys.Control | ModifierKeys.Shift)
					&& FindNext(arg.ctx, t) == false) {
					arg.ctx.HideToolBar();
				}
			});
		}

		void MakeUrl(CommandContext ctx) {
			var t = ctx.View.GetFirstSelectionText();
			if (t.StartsWith("http://", StringComparison.Ordinal) || t.StartsWith("https://", StringComparison.Ordinal)) {
				var s = WrapWith(ctx, "<a href=\"", "\">text</a>", false);
				if (s.Snapshot != null) {
					// select the "text"
					ctx.View.Selection.Select(new SnapshotSpan(s.Snapshot, s.End - 8, 4), false);
					ctx.View.Caret.MoveTo(s.End - 4);
				}
			}
			else {
				var s = WrapWith(ctx, "<a href=\"url\">", "</a>", false);
				if (s.Snapshot != null) {
					// select the "url"
					ctx.View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start + 9, 3), false);
					ctx.View.Caret.MoveTo(s.Start + 12);
				}
			}
		}

		List<CommandItem> GetMarkerCommands(CommandContext arg) {
			var r = new List<CommandItem>(3);
			var symbol = _Symbol;
			if (symbol.Kind == SymbolKind.Method
				&& symbol is IMethodSymbol ctor && ctor.MethodKind == MethodKind.Constructor) {
				symbol = ctor.ContainingType;
			}
			r.Add(new CommandItem(IconIds.MarkSymbol, R.CMD_Mark.Replace("<NAME>", symbol.Name), AddHighlightMenuItems, null));
			if (Taggers.SymbolMarkManager.Contains(symbol)) {
				r.Add(new CommandItem(IconIds.UnmarkSymbol, R.CMD_Unmark.Replace("<NAME>", symbol.Name), UnmarkSymbolMark));
			}
			else if (Taggers.SymbolMarkManager.HasBookmark) {
				r.Add(CreateCommandMenu(IconIds.UnmarkSymbol, R.CMD_UnmarkSymbol, "No symbol marked", (ctx, m) => {
					foreach (var item in Taggers.SymbolMarkManager.MarkedSymbols) {
						m.Items.Add(new CommandMenuItem(this, new CommandItem(item.ImageId, item.DisplayString, _ => {
							Taggers.SymbolMarkManager.Remove(item);
							Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
						})));
					}
				}));
			}
			r.Add(new CommandItem(IconIds.ToggleBreakpoint, R.CMD_ToggleBreakpoint, _ => TextEditorHelper.ExecuteEditorCommand("Debug.ToggleBreakpoint")));
			r.Add(new CommandItem(IconIds.ToggleBookmark, R.CMD_ToggleBookmark, _ => TextEditorHelper.ExecuteEditorCommand("Edit.ToggleBookmark")));
			return r;
		}

		void AddHighlightMenuItems(MenuItem menuItem) {
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 1", item => item.Tag = __HighlightClassifications.Highlight1, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 2", item => item.Tag = __HighlightClassifications.Highlight2, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 3", item => item.Tag = __HighlightClassifications.Highlight3, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 4", item => item.Tag = __HighlightClassifications.Highlight4, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 5", item => item.Tag = __HighlightClassifications.Highlight5, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 6", item => item.Tag = __HighlightClassifications.Highlight6, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 7", item => item.Tag = __HighlightClassifications.Highlight7, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 8", item => item.Tag = __HighlightClassifications.Highlight8, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 9", item => item.Tag = __HighlightClassifications.Highlight9, SetSymbolMark)));
		}

		void SetSymbolMark(CommandContext context) {
			if (_Symbol == null) {
				return;
			}
			if (_Symbol.Kind == SymbolKind.Method
				&& _Symbol is IMethodSymbol ctor && ctor.MethodKind == MethodKind.Constructor) {
				_Symbol = ctor.ContainingType;
			}
			Taggers.SymbolMarkManager.Update(_Symbol, context.Sender.Tag as ClassificationTag);
			Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
		}

		void UnmarkSymbolMark(CommandContext dummy) {
			UpdateSemanticModel();
			if (_Symbol != null && Taggers.SymbolMarkManager.Remove(_Symbol)) {
				Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
			}
		}

		bool UpdateSemanticModel() {
			return SyncHelper.RunSync(UpdateAsync);
		}

		System.Threading.Tasks.Task<bool> UpdateAsync() {
			return _Context.UpdateAsync(View.Selection.Start.Position, default);
		}

		void Menu_Closed(object sender, EventArgs args) {
			var m = sender as ContextMenu;
			m.PlacementTarget = null;
			m.Closed -= Menu_Closed;
			(m as CSharpSymbolContextMenu)?.Dispose();
			HideToolBar();
		}

		void View_Closed(object sender, EventArgs e) {
			(sender as IWpfTextView).Closed -= View_Closed;
			_Context = null;
			_SymbolListContainer = null;
			_Symbol = null;
		}
	}
}

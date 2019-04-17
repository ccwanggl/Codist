﻿using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Operations;
using AppHelpers;
using Microsoft.VisualStudio.Text;

namespace Codist.QuickInfo
{
	static class QuickInfoFactories
	{
		/// <summary>
		/// Provides quick info for named colors or #hex colors
		/// </summary>
		[Export(typeof(IQuickInfoSourceProvider))]
		[Name("Color Quick Info Controller")]
		[Order(After = "Default Quick Info Presenter")]
		[ContentType(Constants.CodeTypes.Text)]
		sealed class ColorQuickInfoControllerProvider : IQuickInfoSourceProvider
		{
			[Import]
			internal ITextStructureNavigatorSelectorService _NavigatorService;

			public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
				return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
					? new ColorQuickInfoController(_NavigatorService)
					: null;
			}
		}

		[Export(typeof(IQuickInfoSourceProvider))]
		[Name(CSharpQuickInfo.Name)]
		[Order(After = "Default Quick Info Presenter")]
		[ContentType(Constants.CodeTypes.CSharp)]
		sealed class CSharpQuickInfoSourceProvider : IQuickInfoSourceProvider
		{
			public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
				return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
					? new CSharpQuickInfo(textBuffer, ServicesHelper.Instance.EditorFormatMap)
					: null;
			}
		}

		/// <summary>
		/// Controls whether quick info should be displayed. When activated, quick info will not be displayed unless Shift key is pressed.
		/// </summary>
		[Export(typeof(IQuickInfoSourceProvider))]
		[Name("Quick Info Visibility Controller")]
		[Order(Before = "Default Quick Info Presenter")]
		[ContentType(Constants.CodeTypes.Code)]
		sealed class QuickInfoVisibilityControllerProvider : IQuickInfoSourceProvider
		{
			public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
				return new QuickInfoVisibilityController();
			}
		}

		/// <summary>Shows information about selections.</summary>
		[Export(typeof(IQuickInfoSourceProvider))]
		[Name(Name)]
		[Order(After = CSharpQuickInfo.Name)]
		[ContentType(Constants.CodeTypes.Text)]
		sealed class SelectionQuickInfoProvider : IQuickInfoSourceProvider
		{
			const string Name = nameof(SelectionQuickInfoProvider);

			public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
				return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
					? new SelectionQuickInfo()
					: null;
			}
		}

#if DEBUG
		/// <summary>Shows information about selections.</summary>
		[Export(typeof(IQuickInfoSourceProvider))]
		[Name(Name)]
		[Order(After = "Default Quick Info Presenter")]
		[ContentType(Constants.CodeTypes.Text)]
		sealed class QuickInfoSizeControllerProvider : IQuickInfoSourceProvider
		{
			const string Name = nameof(QuickInfoSizeController);

			public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
				return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
					? new QuickInfoSizeController()
					: null;
			}
		}
#endif
	}
}

﻿using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.NaviBar
{
	public abstract class NaviBar : ToolBar, INaviBar
	{
		IWpfTextView _View;

		protected NaviBar(IWpfTextView textView) {
			_View = textView;
			_View.Closed += View_Closed;
			ViewOverlay = TextViewOverlay.GetOrCreate(textView);
			this.SetBackgroundForCrispImage(ThemeCache.TitleBackgroundColor);
			textView.Properties.AddProperty(nameof(NaviBar), this);
			Resources = SharedDictionaryManager.NavigationBar;
			UseLayoutRounding = true;
			SnapsToDevicePixels = true;
			if (CodistPackage.VsVersion.Major < 18) {
				SetResourceReference(BackgroundProperty, VsBrushes.CommandBarMenuBackgroundGradientKey);
				SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextInactiveKey);
			}
		}

		public abstract void ShowActiveItemMenu();
		public abstract void ShowRootItemMenu(int parameter);
		internal protected abstract void BindView();
		protected abstract void UnbindView();

		protected IWpfTextView View => _View;
		internal TextViewOverlay ViewOverlay { get; private set; }

		protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e) {
			if ((e.Source as DependencyObject).GetParentOrSelf<DependencyObject>(o => o is IContextMenuHost) is IContextMenuHost h) {
				h.ShowContextMenu(e);
				e.Handled = true;
			}
		}

		protected override AutomationPeer OnCreateAutomationPeer() {
			return null;
		}

		public override void OnApplyTemplate() {
			base.OnApplyTemplate();
			if (CodistPackage.VsVersion.Major >= 18) {
				var b = this.GetFirstVisualChild<StackPanel>();
				if (b != null) {
					b.Background = default;
				}
			}
		}

		void View_Closed(object sender, EventArgs e) {
			if (_View != null) {
				_View.Closed -= View_Closed;
				UnbindView();
				var visualParent = this.GetParent<FrameworkElement>();
				if (visualParent is Panel p) {
					p.Children.Remove(this);
				}
				else if (visualParent is ContentControl c) {
					c.Content = null;
				}
				ViewOverlay = null;
				DataContext = null;
				this.DisposeCollection();
				_View.Properties.RemoveProperty(nameof(NaviBar));
				_View.Properties.RemoveProperty(typeof(TextViewOverlay));
				_View = null;
			}
		}
	}
}

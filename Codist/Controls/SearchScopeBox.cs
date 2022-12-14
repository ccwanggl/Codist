﻿using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
	sealed class SearchScopeBox : UserControl
	{
		readonly ThemedToggleButton _ProjectFilter, _DocumentFilter;
		bool _uiLock;
		private ScopeType _Filter;

		public event EventHandler FilterChanged;

		public SearchScopeBox() {
			_DocumentFilter = CreateButton(IconIds.File, R.T_SearchCurrentDocument);
			_ProjectFilter = CreateButton(IconIds.Project, R.T_SearchCurrentProject);
			Margin = WpfHelper.SmallHorizontalMargin;
			Content = new Border {
				BorderThickness = WpfHelper.TinyMargin,
				CornerRadius = new CornerRadius(3),
				Child = new StackPanel {
					Children = {
						_DocumentFilter, _ProjectFilter,
					},
					Orientation = Orientation.Horizontal
				}
			}.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey);
			_DocumentFilter.IsChecked = true;
		}

		public ScopeType Filter {
			get => _Filter;
			set {
				if (_Filter != value) {
					switch (value) {
						case ScopeType.ActiveDocument: _DocumentFilter.IsChecked = true;
							break;
						case ScopeType.ActiveProject: _ProjectFilter.IsChecked = true;
							break;
					}
				}
			}
		}

		public UIElementCollection Contents => ((StackPanel)((Border)Content).Child).Children;

		ThemedToggleButton CreateButton(int imageId, string toolTip) {
			var b = new ThemedToggleButton(imageId, toolTip).ClearMargin().ClearBorder();
			b.Checked += UpdateFilterValue;
			b.Unchecked += KeepChecked;
			return b;
		}

		void KeepChecked(object sender, RoutedEventArgs e) {
			if (_uiLock) {
				return;
			}
			_uiLock = true;
			(sender as ThemedToggleButton).IsChecked = true;
			e.Handled = true;
			_uiLock = false;
		}

		void UpdateFilterValue(object sender, RoutedEventArgs eventArgs) {
			if (_uiLock) {
				return;
			}
			_uiLock = true;
			_ProjectFilter.IsChecked = _DocumentFilter.IsChecked = false;
			(sender as ThemedToggleButton).IsChecked = true;
			_uiLock = false;
			var f = sender == _DocumentFilter ? ScopeType.ActiveDocument
				: sender == _ProjectFilter ? ScopeType.ActiveProject
				: ScopeType.Undefined;
			if (_Filter != f) {
				_Filter = f;
				FilterChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}
}
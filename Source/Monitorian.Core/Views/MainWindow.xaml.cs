﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Monitorian.Core.Helper;
using Monitorian.Core.Models;
using Monitorian.Core.ViewModels;
using ScreenFrame.Movers;

namespace Monitorian.Core.Views
{
	public partial class MainWindow : Window
	{
		private readonly StickWindowMover _mover;

		public MainWindow(AppControllerCore controller)
		{
			LanguageService.Switch();

			InitializeComponent();

			this.DataContext = new MainWindowViewModel(controller);

			_mover = new StickWindowMover(this, controller.NotifyIconContainer.NotifyIcon);
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);

			WindowEffect.DisableTransitions(this);
			WindowEffect.EnableBackgroundTranslucency(this);
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			CheckDefaultHeights();

			BindingOperations.SetBinding(
				this,
				UsesLargeElementsProperty,
				new Binding(nameof(SettingsCore.UsesLargeElements))
				{
					Source = ((MainWindowViewModel)this.DataContext).Settings,
					Mode = BindingMode.OneWay
				});

			//this.InvalidateProperty(UsesLargeElementsProperty);
		}

		protected override void OnClosed(EventArgs e)
		{
			BindingOperations.ClearBinding(
				this,
				UsesLargeElementsProperty);

			base.OnClosed(e);
		}

		#region Elements

		private const double ShrinkFactor = 0.6;
		private Dictionary<string, double> _defaultHeights;

		private void CheckDefaultHeights()
		{
			_defaultHeights = this.Resources.Cast<DictionaryEntry>()
				.Where(x => ((string)x.Key).EndsWith("Height", StringComparison.Ordinal))
				.Where(x => (x.Value is double height) && (0 < height))
				.ToDictionary(x => (string)x.Key, x => (double)x.Value);
		}

		public bool UsesLargeElements
		{
			get { return (bool)GetValue(UsesLargeElementsProperty); }
			set { SetValue(UsesLargeElementsProperty, value); }
		}
		public static readonly DependencyProperty UsesLargeElementsProperty =
			DependencyProperty.Register(
				"UsesLargeElements",
				typeof(bool),
				typeof(MainWindow),
				new PropertyMetadata(
					true,
					(d, e) =>
					{
						// Setting the same value will not trigger calling this method.					

						var window = (MainWindow)d;
						if (window._defaultHeights is null)
							return;

						var factor = (bool)e.NewValue ? 1D : ShrinkFactor;

						foreach (var (key, value) in window._defaultHeights)
						{
							window.Resources[key] = value * factor;
						}
					}));

		#endregion

		#region Show/Hide

		public bool IsForeground => _mover.IsForeground();

		public void ShowForeground()
		{
			try
			{
				this.Topmost = true;

				base.Show();
			}
			finally
			{
				this.Topmost = false;
			}
		}

		public bool CanBeShown => (_preventionTime < DateTimeOffset.Now);
		private DateTimeOffset _preventionTime;

		protected override async void OnDeactivated(EventArgs e)
		{
			base.OnDeactivated(e);

			if (this.Visibility != Visibility.Visible)
				return;

			// Set time to prevent this window from being shown unintentionally. 
			_preventionTime = DateTimeOffset.Now + TimeSpan.FromSeconds(0.2);

			// Clear focus.
			FocusManager.SetFocusedElement(this, null);

			// Wait for this window to be refreshed before being hidden.
			await Task.Delay(TimeSpan.FromSeconds(0.1));

			this.Hide();
		}

		#endregion
	}
}
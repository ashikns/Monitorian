﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interactivity;

namespace Monitorian.Core.Views.Behaviors
{
	public class FocusElementAction : TriggerAction<DependencyObject>
	{
		public UIElement TargetElement
		{
			get { return (UIElement)GetValue(TargetElementProperty); }
			set { SetValue(TargetElementProperty, value); }
		}
		public static readonly DependencyProperty TargetElementProperty =
			DependencyProperty.Register(
				"TargetElement",
				typeof(UIElement),
				typeof(FocusElementAction),
				new PropertyMetadata(null));

		protected override void Invoke(object parameter)
		{
			if ((TargetElement is null) || !TargetElement.Focusable || TargetElement.IsFocused)
				return;

			TargetElement.Focus();
		}
	}
}
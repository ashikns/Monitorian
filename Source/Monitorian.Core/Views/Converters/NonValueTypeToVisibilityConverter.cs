using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Monitorian.Core.Views.Converters
{
	[ValueConversion(typeof(object), typeof(Visibility))]
	public class NonValueTypeToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is null)
				return Visibility.Collapsed;

			var type = value.GetType();
			if (type.IsValueType || type.IsEnum)
				return DependencyProperty.UnsetValue;

			if ((type == typeof(string)) && string.IsNullOrWhiteSpace((string)value))
				return Visibility.Collapsed;

			return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
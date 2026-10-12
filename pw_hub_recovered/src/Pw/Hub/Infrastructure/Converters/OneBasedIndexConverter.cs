using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Pw.Hub.Infrastructure.Converters;

public class OneBasedIndexConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		try
		{
			if (value == null || value == DependencyProperty.UnsetValue)
			{
				return string.Empty;
			}
			if (value is int num)
			{
				return (num + 1).ToString(culture) + ".";
			}
			if (int.TryParse(value.ToString(), out var result))
			{
				return (result + 1).ToString(culture) + ".";
			}
		}
		catch
		{
		}
		return string.Empty;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}

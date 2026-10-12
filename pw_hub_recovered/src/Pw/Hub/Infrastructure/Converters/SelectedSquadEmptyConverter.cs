using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Pw.Hub.Infrastructure.Converters;

public class SelectedSquadEmptyConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		try
		{
			if (values == null || values.Length < 3)
			{
				return Visibility.Collapsed;
			}
			object obj = values[0];
			object obj2 = values[1];
			object obj3 = values[2];
			if (obj != obj2)
			{
				return Visibility.Collapsed;
			}
			int num = 0;
			int result;
			if (obj3 is int num2)
			{
				num = num2;
			}
			else if (obj3 is string s && int.TryParse(s, out result))
			{
				num = result;
			}
			return (num != 0) ? Visibility.Collapsed : Visibility.Visible;
		}
		catch
		{
			return Visibility.Collapsed;
		}
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}

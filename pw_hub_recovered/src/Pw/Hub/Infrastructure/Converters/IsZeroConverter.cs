using System;
using System.Globalization;
using System.Windows.Data;

namespace Pw.Hub.Infrastructure.Converters;

public class IsZeroConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		try
		{
			if (value == null)
			{
				return false;
			}
			if (value is int num)
			{
				return num == 0;
			}
			if (value is long num2)
			{
				return num2 == 0;
			}
			if (value is double value2)
			{
				return Math.Abs(value2) < double.Epsilon;
			}
			if (value is float value3)
			{
				return Math.Abs(value3) < float.Epsilon;
			}
			if (value is decimal num3)
			{
				return num3 == 0m;
			}
			if (value is string s && int.TryParse(s, out var result))
			{
				return result == 0;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}

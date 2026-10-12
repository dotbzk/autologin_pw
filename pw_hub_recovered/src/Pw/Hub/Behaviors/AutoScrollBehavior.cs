using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Pw.Hub.Behaviors;

public static class AutoScrollBehavior
{
	public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(AutoScrollBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

	public static void SetIsEnabled(DependencyObject element, bool value)
	{
		element.SetValue(IsEnabledProperty, value);
	}

	public static bool GetIsEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(IsEnabledProperty);
	}

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is TextBox textBox)
		{
			if ((bool)e.NewValue)
			{
				textBox.TextChanged += TbOnTextChanged;
			}
			else
			{
				textBox.TextChanged -= TbOnTextChanged;
			}
		}
		else
		{
			_ = d is ScrollViewer;
		}
	}

	private static void TbOnTextChanged(object sender, TextChangedEventArgs e)
	{
		if (!(sender is TextBox textBox))
		{
			return;
		}
		try
		{
			if (!textBox.Dispatcher.CheckAccess())
			{
				textBox.Dispatcher.BeginInvoke(new Action<object, TextChangedEventArgs>(TbOnTextChanged), DispatcherPriority.Background, sender, e);
				return;
			}
			textBox.CaretIndex = textBox.Text?.Length ?? 0;
			textBox.UpdateLayout();
			textBox.ScrollToEnd();
			ScrollViewer scrollViewer = FindParentScrollViewer(textBox);
			try
			{
				scrollViewer?.ScrollToBottom();
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private static ScrollViewer? FindParentScrollViewer(DependencyObject child)
	{
		for (DependencyObject parent = VisualTreeHelper.GetParent(child); parent != null; parent = VisualTreeHelper.GetParent(parent))
		{
			if (parent is ScrollViewer result)
			{
				return result;
			}
		}
		return null;
	}
}

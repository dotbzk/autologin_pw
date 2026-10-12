using System.Windows;

namespace Pw.Hub.Services;

public class WindowService : IWindowService
{
	public void ShowWindow(Window window)
	{
		if (Application.Current?.MainWindow != null && window.Owner == null)
		{
			window.Owner = Application.Current.MainWindow;
		}
		window.Show();
	}

	public bool? ShowDialog(Window window, Window? owner = null)
	{
		if (owner != null)
		{
			window.Owner = owner;
		}
		else if (Application.Current?.MainWindow != null && window.Owner == null)
		{
			window.Owner = Application.Current.MainWindow;
		}
		return window.ShowDialog();
	}
}

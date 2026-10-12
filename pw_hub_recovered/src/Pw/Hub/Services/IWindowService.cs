using System.Windows;

namespace Pw.Hub.Services;

public interface IWindowService
{
	void ShowWindow(Window window);

	bool? ShowDialog(Window window, Window? owner = null);
}

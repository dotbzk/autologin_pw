using System;
using System.Windows;
using System.Windows.Input;

namespace Pw.Hub;

public static class WindowBehaviors
{
	private class RelayCommand : ICommand
	{
		private readonly Action<object> _execute;

		public event EventHandler CanExecuteChanged;

		public RelayCommand(Action<object> execute)
		{
			_execute = execute;
		}

		public bool CanExecute(object parameter)
		{
			return true;
		}

		public void Execute(object parameter)
		{
			_execute(parameter);
		}
	}

	public static readonly ICommand MinimizeCommand = new RelayCommand(delegate(object obj)
	{
		if (obj is Window window)
		{
			window.WindowState = WindowState.Minimized;
		}
	});

	public static readonly ICommand MaximizeRestoreCommand = new RelayCommand(delegate(object obj)
	{
		if (obj is Window window)
		{
			window.WindowState = ((window.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
		}
	});

	public static readonly ICommand CloseCommand = new RelayCommand(delegate(object obj)
	{
		if (obj is Window window)
		{
			window.Close();
		}
	});
}

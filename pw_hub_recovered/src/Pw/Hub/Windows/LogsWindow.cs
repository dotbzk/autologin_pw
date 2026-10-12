using System;
using System.CodeDom.Compiler;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Pw.Hub.Infrastructure.Logging;

namespace Pw.Hub.Windows;

public class LogsWindow : Window, IComponentConnector
{
	private static LogsWindow? _instance;

	private readonly ObservableCollection<LogEntry> _items = new ObservableCollection<LogEntry>();

	internal ComboBox LevelFilter;

	internal TextBox CategoryFilter;

	internal CheckBox AutoScroll;

	internal ListView List;

	private bool _contentLoaded;

	public LogsWindow()
	{
		InitializeComponent();
		List.ItemsSource = _items;
		base.Closing += delegate(object? _, CancelEventArgs e)
		{
			e.Cancel = true;
			Hide();
		};
		try
		{
			Log.EntryPublished += OnEntry;
		}
		catch
		{
		}
		LevelFilter.SelectionChanged += delegate
		{
			ApplyFilter();
		};
		CategoryFilter.TextChanged += delegate
		{
			ApplyFilter();
		};
	}

	private LogLevel MinLevelFromUi()
	{
		return LevelFilter.SelectedIndex switch
		{
			0 => LogLevel.Debug,
			1 => LogLevel.Information,
			2 => LogLevel.Warning,
			3 => LogLevel.Error,
			_ => LogLevel.Information,
		};
	}

	private void OnEntry(LogEntry e)
	{
		try
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				if (e.Level >= MinLevelFromUi())
				{
					string value = (CategoryFilter.Text ?? string.Empty).Trim();
					if (string.IsNullOrWhiteSpace(value) || (e.Category?.IndexOf(value, StringComparison.OrdinalIgnoreCase) ?? (-1)) >= 0)
					{
						_items.Add(e);
						if (_items.Count > 2000)
						{
							while (_items.Count > 2000)
							{
								_items.RemoveAt(0);
							}
						}
						if (AutoScroll.IsChecked == true && _items.Count > 0)
						{
							try
							{
								ListView list = List;
								ObservableCollection<LogEntry> items = _items;
								list.ScrollIntoView(items[items.Count - 1]);
							}
							catch
							{
							}
						}
					}
				}
			});
		}
		catch
		{
		}
	}

	private void ApplyFilter()
	{
		try
		{
			_items.Clear();
		}
		catch
		{
		}
	}

	public static void Toggle(Window? owner = null)
	{
		try
		{
			if (_instance == null)
			{
				_instance = new LogsWindow();
				if (owner != null)
				{
					_instance.Owner = owner;
				}
			}
			if (_instance.IsVisible)
			{
				_instance.Hide();
			}
			else
			{
				_instance.Show();
			}
		}
		catch
		{
		}
	}

	public static void ShowOnDebug(Window? owner = null)
	{
		try
		{
			if (!RuntimeOptions.DebugMode)
			{
				return;
			}
			if (_instance == null)
			{
				_instance = new LogsWindow();
				if (owner != null)
				{
					_instance.Owner = owner;
				}
			}
			_instance.Show();
		}
		catch
		{
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/Pw.Hub;component/windows/logswindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			LevelFilter = (ComboBox)target;
			break;
		case 2:
			CategoryFilter = (TextBox)target;
			break;
		case 3:
			AutoScroll = (CheckBox)target;
			break;
		case 4:
			List = (ListView)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

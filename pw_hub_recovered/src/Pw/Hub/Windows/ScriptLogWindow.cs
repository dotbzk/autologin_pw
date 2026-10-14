using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Pw.Hub.ViewModels;

namespace Pw.Hub.Windows;

public class ScriptLogWindow : Window, IComponentConnector
{
	private readonly bool _closeWhenEnd;

	internal ScrollViewer Scroll;

	internal TextBox LogTextBox;

	private bool _contentLoaded;

	public ScriptLogViewModel Vm { get; }

	public ScriptLogWindow(string moduleTitle = null, bool closeWhenEnd = false)
	{
		_closeWhenEnd = closeWhenEnd;
		InitializeComponent();
		Vm = new ScriptLogViewModel
		{
			Title = (string.IsNullOrWhiteSpace(moduleTitle) ? "Execution Logs" : ("Module — " + moduleTitle))
		};
		Vm.RequestClose += OnRequestClose;
		base.DataContext = Vm;
	}

	public void SetStopAction(Action onStop)
	{
		Vm.SetStopAction(onStop);
	}

	public void SetRunning(bool running)
	{
		Vm.SetRunning(running);
	}

	public void AppendLog(string line)
	{
		Vm.AppendLog(line);
	}

	public void ReportProgress(int percent, string message = null)
	{
		Vm.ReportProgress(percent, message);
		base.TaskbarItemInfo.ProgressValue = (double)percent / 100.0;
	}

	public void MarkCompleted(string finalMessage = null)
	{
		Vm.MarkCompleted(finalMessage);
		if (_closeWhenEnd)
		{
			try
			{
				base.DialogResult = true;
			}
			catch
			{
			}
			Close();
		}
	}

	private void OnRequestClose()
	{
		try
		{
			base.DialogResult = true;
		}
		catch
		{
		}
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/Pw.Hub;component/windows/scriptlogwindow.xaml", UriKind.Relative);
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
			Scroll = (ScrollViewer)target;
			break;
		case 2:
			LogTextBox = (TextBox)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

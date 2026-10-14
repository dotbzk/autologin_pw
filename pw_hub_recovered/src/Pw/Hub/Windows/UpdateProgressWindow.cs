using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Pw.Hub.Windows;

public class UpdateProgressWindow : Window, IComponentConnector
{
	internal TextBlock TitleText;

	internal ProgressBar Progress;

	internal TextBlock PercentText;

	internal TextBlock StatusText;

	private bool _contentLoaded;

	public UpdateProgressWindow()
	{
		InitializeComponent();
		SetIndeterminate(indeterminate: true);
		SetStatus("Preparing...");
	}

	public void SetTitle(string title)
	{
		if (!CheckAccess())
		{
			base.Dispatcher.Invoke(delegate
			{
				SetTitle(title);
			});
		}
		else
		{
			TitleText.Text = title;
		}
	}

	public void SetStatus(string status)
	{
		if (!CheckAccess())
		{
			base.Dispatcher.Invoke(delegate
			{
				SetStatus(status);
			});
		}
		else
		{
			StatusText.Text = status;
		}
	}

	public void SetIndeterminate(bool indeterminate)
	{
		if (!CheckAccess())
		{
			base.Dispatcher.Invoke(delegate
			{
				SetIndeterminate(indeterminate);
			});
		}
		else
		{
			Progress.IsIndeterminate = indeterminate;
			PercentText.Text = (indeterminate ? string.Empty : "0%");
		}
	}

	public void SetProgress(double percent)
	{
		if (!CheckAccess())
		{
			base.Dispatcher.Invoke(delegate
			{
				SetProgress(percent);
			});
			return;
		}
		if (percent < 0.0)
		{
			percent = 0.0;
		}
		if (percent > 100.0)
		{
			percent = 100.0;
		}
		Progress.IsIndeterminate = false;
		Progress.Value = percent;
		PercentText.Text = Math.Round(percent) + "%";
	}

	public void CloseSafe()
	{
		if (!CheckAccess())
		{
			base.Dispatcher.Invoke(CloseSafe);
			return;
		}
		try
		{
			Close();
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
			Uri resourceLocator = new Uri("/Pw.Hub;component/windows/updateprogresswindow.xaml", UriKind.Relative);
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
			TitleText = (TextBlock)target;
			break;
		case 2:
			Progress = (ProgressBar)target;
			break;
		case 3:
			PercentText = (TextBlock)target;
			break;
		case 4:
			StatusText = (TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

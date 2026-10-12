using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;
using Pw.Hub.ViewModels;

namespace Pw.Hub.Windows;

public class DebugVariablesWindow : Window, IComponentConnector
{
	private bool _contentLoaded;

	public DebugVariablesViewModel Vm { get; }

	public DebugVariablesWindow()
	{
		InitializeComponent();
		Vm = new DebugVariablesViewModel();
		Vm.RequestClose += OnRequestClose;
		base.DataContext = Vm;
	}

	public void SetData(int line, IDictionary<string, object> locals, IDictionary<string, object> globals)
	{
		Vm.SetData(line, locals, globals);
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
			Uri resourceLocator = new Uri("/Pw.Hub;component/windows/debugvariableswindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		_contentLoaded = true;
	}
}

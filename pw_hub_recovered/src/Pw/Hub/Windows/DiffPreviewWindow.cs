using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Pw.Hub.ViewModels;

namespace Pw.Hub.Windows;

public class DiffPreviewWindow : Window, IComponentConnector
{
	internal RichTextBox DiffBox;

	private bool _contentLoaded;

	public IList<string> DiffLines { get; }

	public DiffPreviewWindow(IList<string> lines)
	{
		DiffLines = lines ?? new List<string>();
		InitializeComponent();
		base.DataContext = this;
	}

	public DiffPreviewWindow(LuaEditorAiViewModel aiVm)
	{
		InitializeComponent();
		base.DataContext = aiVm;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/Pw.Hub;component/windows/diffpreviewwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		if (connectionId == 1)
		{
			DiffBox = (RichTextBox)target;
		}
		else
		{
			_contentLoaded = true;
		}
	}
}

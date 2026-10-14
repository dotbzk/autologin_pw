using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Pw.Hub.Models;

namespace Pw.Hub.Pages;

public class EditSquadWindow : Window, IComponentConnector
{
	private readonly Squad _squad;

	internal TextBox SquadNameTextBox;

	private bool _contentLoaded;

	public string SquadName { get; private set; } = string.Empty;

	public EditSquadWindow(Squad squad)
	{
		InitializeComponent();
		_squad = squad;
		SquadNameTextBox.Text = squad.Name;
		SquadNameTextBox.SelectAll();
		SquadNameTextBox.Focus();
	}

	private void OnSaveClick(object sender, RoutedEventArgs e)
	{
		string text = SquadNameTextBox.Text.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			MessageBox.Show("Enter a squad name.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		SquadName = text;
		base.DialogResult = true;
		Close();
	}

	private void OnCancelClick(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/Pw.Hub;component/pages/editsquadwindow.xaml", UriKind.Relative);
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
			SquadNameTextBox = (TextBox)target;
			break;
		case 2:
			((Button)target).Click += OnCancelClick;
			break;
		case 3:
			((Button)target).Click += OnSaveClick;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Pw.Hub.Models;

namespace Pw.Hub.Pages;

public class EditAccountWindow : Window, IComponentConnector
{
	private readonly Account _account;

	internal TextBox AccountNameTextBox;

	internal ItemsControl ServersList;

	private bool _contentLoaded;

	public string AccountName { get; private set; } = string.Empty;

	public string Email { get; private set; }

	public EditAccountWindow(Account account)
	{
		InitializeComponent();
		_account = account;
		AccountNameTextBox.Text = account.Name;
		ServersList.ItemsSource = _account.Servers ?? new List<AccountServer>();
		AccountNameTextBox.SelectAll();
		AccountNameTextBox.Focus();
	}

	private void OnSaveClick(object sender, RoutedEventArgs e)
	{
		string text = AccountNameTextBox.Text.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			MessageBox.Show("Введите название аккаунта", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		AccountName = text;
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
			Uri resourceLocator = new Uri("/Pw.Hub;component/pages/editaccountwindow.xaml", UriKind.Relative);
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
			AccountNameTextBox = (TextBox)target;
			break;
		case 2:
			ServersList = (ItemsControl)target;
			break;
		case 3:
			((Button)target).Click += OnCancelClick;
			break;
		case 4:
			((Button)target).Click += OnSaveClick;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Pw.Hub.Services;

namespace Pw.Hub.Windows;

public class LoginRegisterWindow : Window, IComponentConnector
{
	private readonly ModulesApiClient _api;

	private readonly bool _skipAutoLogin;

	internal TextBox UsernameText;

	internal PasswordBox PasswordBox;

	internal CheckBox RememberCheck;

	internal Border ErrorBorder;

	internal TextBlock ErrorText;

	private bool _contentLoaded;

	public LoginRegisterWindow(bool skipAutoLogin = false)
	{
		InitializeComponent();
		_api = new ModulesApiClient();
		_skipAutoLogin = skipAutoLogin;
		base.Loaded += async delegate
		{
			await TryAutoLoginAsync();
		};
	}

	private async Task TryAutoLoginAsync()
	{
		if (_skipAutoLogin)
		{
			return;
		}
		try
		{
			if (!string.IsNullOrWhiteSpace(AuthState.Token) && await _api.MeAsync() != null)
			{
				base.DialogResult = true;
				Close();
			}
		}
		catch
		{
		}
	}

	private async void RegisterClick(object sender, RoutedEventArgs e)
	{
		try
		{
			string username = UsernameText.Text?.Trim() ?? string.Empty;
			string password = PasswordBox.Password ?? string.Empty;
			if (await _api.RegisterAsync(username, password) == null)
			{
				ErrorText.Text = "Registration failed.";
				ErrorBorder.Visibility = Visibility.Visible;
				return;
			}
			ErrorBorder.Visibility = Visibility.Collapsed;
			bool valueOrDefault = RememberCheck.IsChecked == true;
			AuthState.Set(_api.Token, _api.CurrentUser, valueOrDefault);
			base.DialogResult = true;
			Close();
		}
		catch (Exception ex)
		{
			ErrorText.Text = ex.Message;
			ErrorBorder.Visibility = Visibility.Visible;
		}
	}

	private async void LoginClick(object sender, RoutedEventArgs e)
	{
		try
		{
			string username = UsernameText.Text?.Trim() ?? string.Empty;
			string password = PasswordBox.Password ?? string.Empty;
			if (await _api.LoginAsync(username, password) == null)
			{
				ErrorText.Text = "Sign-in failed.";
				ErrorBorder.Visibility = Visibility.Visible;
				return;
			}
			ErrorBorder.Visibility = Visibility.Collapsed;
			bool valueOrDefault = RememberCheck.IsChecked == true;
			AuthState.Set(_api.Token, _api.CurrentUser, valueOrDefault);
			base.DialogResult = true;
			Close();
		}
		catch (Exception ex)
		{
			ErrorText.Text = ex.Message;
			ErrorBorder.Visibility = Visibility.Visible;
		}
	}

	private void CancelClick(object sender, RoutedEventArgs e)
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
			Uri resourceLocator = new Uri("/Pw.Hub;component/windows/loginregisterwindow.xaml", UriKind.Relative);
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
			UsernameText = (TextBox)target;
			break;
		case 2:
			PasswordBox = (PasswordBox)target;
			break;
		case 3:
			RememberCheck = (CheckBox)target;
			break;
		case 4:
			ErrorBorder = (Border)target;
			break;
		case 5:
			ErrorText = (TextBlock)target;
			break;
		case 6:
			((Button)target).Click += CancelClick;
			break;
		case 7:
			((Button)target).Click += RegisterClick;
			break;
		case 8:
			((Button)target).Click += LoginClick;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

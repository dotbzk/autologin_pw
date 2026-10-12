using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using Pw.Hub.Services;

namespace Pw.Hub.Windows;

public class ProfileEditWindow : Window, IComponentConnector
{
	private readonly ModulesApiClient _api;

	private readonly DispatcherTimer _pollTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(5.0)
	};

	private bool _isPolling;

	internal TextBox UsernameBox;

	internal Button SaveUsernameBtn;

	internal PasswordBox CurrentPasswordBox;

	internal PasswordBox NewPasswordBox;

	internal PasswordBox ConfirmPasswordBox;

	internal Button ChangePasswordBtn;

	internal TextBlock TelegramStatusText;

	internal Button LinkTelegramBtn;

	internal Button UnlinkTelegramBtn;

	private bool _contentLoaded;

	public ProfileEditWindow()
	{
		InitializeComponent();
		_api = new ModulesApiClient();
		base.Loaded += async delegate
		{
			await LoadUser();
			try
			{
				_pollTimer.Start();
			}
			catch
			{
			}
		};
		_pollTimer.Tick += async delegate
		{
			await RefreshBindingSafeAsync();
		};
		base.Closed += delegate
		{
			try
			{
				_pollTimer.Stop();
			}
			catch
			{
			}
		};
	}

	private void UpdateTelegramUi(UserDto me)
	{
		if (me != null && me.TelegramId.HasValue)
		{
			TelegramStatusText.Text = "Привязан: @" + (me.TelegramUsername ?? me.TelegramId.ToString());
			LinkTelegramBtn.IsEnabled = false;
			UnlinkTelegramBtn.IsEnabled = true;
		}
		else
		{
			TelegramStatusText.Text = "Не привязан";
			LinkTelegramBtn.IsEnabled = true;
			UnlinkTelegramBtn.IsEnabled = false;
		}
	}

	private async Task RefreshBindingSafeAsync()
	{
		if (_isPolling)
		{
			return;
		}
		_isPolling = true;
		try
		{
			UserDto userDto = await _api.MeAsync();
			if (userDto != null)
			{
				UpdateTelegramUi(userDto);
			}
		}
		catch
		{
		}
		finally
		{
			_isPolling = false;
		}
	}

	private async void LinkTelegramBtn_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			ModulesApiClient.TelegramLinkInfo telegramLinkInfo = await _api.GenerateTelegramLinkAsync();
			if (telegramLinkInfo == null)
			{
				MessageBox.Show("Не удалось получить ссылку для привязки Telegram.", "Telegram", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			try
			{
				Process.Start(new ProcessStartInfo(telegramLinkInfo.Link)
				{
					UseShellExecute = true
				});
			}
			catch
			{
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Ошибка: " + ex.Message, "Telegram", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async void UnlinkTelegramBtn_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			UserDto userDto = await _api.UnlinkTelegramAsync();
			if (userDto == null)
			{
				MessageBox.Show("Не удалось отвязать Telegram.", "Telegram", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			else
			{
				UpdateTelegramUi(userDto);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Ошибка: " + ex.Message, "Telegram", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void SwitchAccountBtn_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			if (new LoginRegisterWindow(skipAutoLogin: true)
			{
				Owner = this
			}.ShowDialog() == true)
			{
				base.DialogResult = true;
				Close();
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Ошибка: " + ex.Message, "Смена аккаунта", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async Task LoadUser()
	{
		try
		{
			UserDto userDto = await _api.MeAsync();
			if (userDto != null)
			{
				UsernameBox.Text = userDto.Username ?? string.Empty;
				UpdateTelegramUi(userDto);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Ошибка загрузки профиля: " + ex.Message, "Профиль", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async void SaveUsernameBtn_OnClick(object sender, RoutedEventArgs e)
	{
		string text = (UsernameBox.Text ?? string.Empty).Trim();
		if (text.Length < 3)
		{
			MessageBox.Show("Имя пользователя должно содержать не менее 3 символов", "Профиль", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		try
		{
			if (await _api.UpdateUsernameAsync(text) == null)
			{
				MessageBox.Show("Не удалось сохранить имя. Возможно, имя занято.", "Профиль", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			else
			{
				MessageBox.Show("Имя пользователя обновлено", "Профиль", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Ошибка: " + ex.Message, "Профиль", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async void ChangePasswordBtn_OnClick(object sender, RoutedEventArgs e)
	{
		string currentPassword = CurrentPasswordBox.Password ?? string.Empty;
		string text = NewPasswordBox.Password ?? string.Empty;
		string b = ConfirmPasswordBox.Password ?? string.Empty;
		if (text.Length < 3)
		{
			MessageBox.Show("Новый пароль слишком короткий", "Смена пароля", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (!string.Equals(text, b, StringComparison.Ordinal))
		{
			MessageBox.Show("Пароли не совпадают", "Смена пароля", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		try
		{
			if (!(await _api.ChangePasswordAsync(currentPassword, text)))
			{
				MessageBox.Show("Не удалось изменить пароль. Проверьте текущий пароль.", "Смена пароля", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			CurrentPasswordBox.Password = string.Empty;
			NewPasswordBox.Password = string.Empty;
			ConfirmPasswordBox.Password = string.Empty;
			MessageBox.Show("Пароль изменён", "Смена пароля", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Ошибка: " + ex.Message, "Смена пароля", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void Close_OnClick(object sender, RoutedEventArgs e)
	{
		base.DialogResult = true;
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/Pw.Hub;component/windows/profileeditwindow.xaml", UriKind.Relative);
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
			UsernameBox = (TextBox)target;
			break;
		case 2:
			SaveUsernameBtn = (Button)target;
			SaveUsernameBtn.Click += SaveUsernameBtn_OnClick;
			break;
		case 3:
			CurrentPasswordBox = (PasswordBox)target;
			break;
		case 4:
			NewPasswordBox = (PasswordBox)target;
			break;
		case 5:
			ConfirmPasswordBox = (PasswordBox)target;
			break;
		case 6:
			ChangePasswordBtn = (Button)target;
			ChangePasswordBtn.Click += ChangePasswordBtn_OnClick;
			break;
		case 7:
			TelegramStatusText = (TextBlock)target;
			break;
		case 8:
			LinkTelegramBtn = (Button)target;
			LinkTelegramBtn.Click += LinkTelegramBtn_OnClick;
			break;
		case 9:
			UnlinkTelegramBtn = (Button)target;
			UnlinkTelegramBtn.Click += UnlinkTelegramBtn_OnClick;
			break;
		case 10:
			((Button)target).Click += SwitchAccountBtn_OnClick;
			break;
		case 11:
			((Button)target).Click += Close_OnClick;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

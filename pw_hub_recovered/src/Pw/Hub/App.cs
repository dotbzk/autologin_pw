using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NotifyIconEx;
using Pw.Hub.Infrastructure.Logging;
using Pw.Hub.Models;
using Pw.Hub.Services;
using Pw.Hub.ViewModels;
using Pw.Hub.Windows;

namespace Pw.Hub;

public class App : System.Windows.Application
{
	private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	public NotifyIconEx.NotifyIcon NotifyIcon;

	private AuthService _authService;

	private BrowserSessionBridge? _browserSessionBridge;

	private static Mutex _singleInstanceMutex;

	private bool _contentLoaded;

	public static IServiceProvider Services { get; private set; }

	protected override void OnStartup(StartupEventArgs e)
	{
		try
		{
			_singleInstanceMutex = new Mutex(initiallyOwned: true, "Global\\Pw.Hub_SingleInstance", out var createdNew);
			if (!createdNew)
			{
				try
				{
					ActivateExistingInstance();
				}
				catch
				{
				}
				System.Windows.Application.Current.Shutdown();
				return;
			}
		}
		catch
		{
		}
		base.OnStartup(e);
		try
		{
			Directory.SetCurrentDirectory(AppContext.BaseDirectory);
		}
		catch
		{
		}
		try
		{
			bool flag = (RuntimeOptions.DebugMode = (e?.Args ?? Array.Empty<string>()).Any((string a) => string.Equals(a, "--debug", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-d", StringComparison.OrdinalIgnoreCase)));
			Log.Initialize(new LoggerConfig
			{
				LogsDirectory = StartupDiagnostics.GetLogsDirectory(),
				MinimumLevel = (flag ? LogLevel.Debug : LogLevel.Information),
				MaxQueueSize = 10000
			});
			Log.For<App>().Info($"App starting. DebugMode={flag}");
			base.DispatcherUnhandledException += App_OnDispatcherUnhandledException;
			AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs ev)
			{
				try
				{
					Log.For<App>().Critical("Unhandled domain exception", ev.ExceptionObject as Exception);
				}
				catch
				{
				}
			};
			TaskScheduler.UnobservedTaskException += delegate(object? s, UnobservedTaskExceptionEventArgs ev)
			{
				try
				{
					Log.For<App>().Error("Unobserved task exception", ev.Exception);
				}
				catch
				{
				}
			};
		}
		catch
		{
		}
		try
		{
			ServiceCollection services = new ServiceCollection();
			services.AddSingleton<IWindowService, WindowService>();
			services.AddSingleton<MainViewModel>();
			services.AddSingleton<AuthService>();
			services.AddSingleton<IUpdatesCheckService, UpdatesCheckService>();
			services.AddSingleton<IModulesSyncService, ModulesSyncService>();
			services.AddSingleton<ILuaExecutionService, LuaExecutionService>();
			services.AddSingleton<ILuaDebugService, LuaDebugService>();
			services.AddSingleton<ICharactersLoadService, CharactersLoadService>();
			services.AddSingleton<IRunModuleCoordinator, RunModuleCoordinator>();
			services.AddSingleton<IUiDialogService, UiDialogService>();
			services.AddSingleton<IOrderingService, OrderingService>();
			services.AddSingleton<IAccountsService, AccountsService>();
			services.AddSingleton<IAppConfigService, JsonAppConfigService>();
			services.AddSingleton<IAiConfigService, AiConfigService>();
			services.AddSingleton<IDiffPreviewService, DiffPreviewService>();
			services.AddSingleton<IAiAssistantService, AiAssistantService>();
			services.AddSingleton<IAiDocService, AiDocService>();
			Services = services.BuildServiceProvider();
		}
		catch
		{
		}
		NotifyIcon = new NotifyIconEx.NotifyIcon
		{
			Text = "PW Hub",
			Icon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule?.FileName)
		};
		_authService = Services.GetRequiredService<AuthService>();
		NotifyIcon.AddMenu("Проверить авторизацию", OnClick);
		NotifyIcon.AddMenu("Подключить расширение браузера", OnConnectBrowserExtension);
		NotifyIcon.AddMenu("Проверить обновления", delegate
		{
			System.Windows.MessageBox.Show(
				"Обновления управляются через Game Tools Hub.",
				"PW Account Hub",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
		});
		NotifyIcon.AddMenu("-");
		NotifyIcon.AddMenu("Выход", async delegate
		{
			try
			{
				if (System.Windows.Application.Current.MainWindow is MainWindow window)
				{
					await window.AccountPage.AccountManager.SaveCurrentSessionAsync();
				}
			}
			catch
			{
			}
			System.Windows.Application.Current.Shutdown();
		});
		NotifyIcon.MouseDoubleClick += NotifyIconOnMouseDoubleClick;
		System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		if (new LoginRegisterWindow().ShowDialog() != true)
		{
			System.Windows.Application.Current.Shutdown();
			return;
		}
		MainWindow mainWindow = new MainWindow();
		System.Windows.Application.Current.MainWindow = mainWindow;
		mainWindow.Show();
		BrowserSessionBridge? bridge = null;
		try
		{
			bridge = new BrowserSessionBridge(
				() => mainWindow.AccountPage?.AccountManager?.CurrentAccount,
				(Cookie[] cookies) => mainWindow.Dispatcher
					.InvokeAsync(() => mainWindow.AccountPage.AccountManager.ImportBrowserSessionAsync(cookies))
					.Task.Unwrap(),
				(string accountId) => mainWindow.Dispatcher
					.InvokeAsync(() => mainWindow.AccountPage.AccountManager.ExportBrowserSessionAsync(accountId))
					.Task.Unwrap());
			bridge.Start();
			_browserSessionBridge = bridge;
		}
		catch (Exception ex)
		{
			bridge?.Dispose();
			_browserSessionBridge = null;
			Log.For<App>().Warn("Не удалось запустить мост расширения браузера", new Dictionary<string, object> { { "error", ex.Message } });
		}
		try
		{
			LogsWindow.ShowOnDebug(mainWindow);
		}
		catch
		{
		}
		System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
	}

	private void NotifyIconOnMouseDoubleClick(object sender, MouseEventArgs e)
	{
		System.Windows.Application.Current.MainWindow?.Show();
		System.Windows.Application.Current.MainWindow?.Activate();
	}

	private async void OnClick(object sender, EventArgs e)
	{
		await _authService.CheckAccounts(ignoreVisibility: true);
	}

	private void OnConnectBrowserExtension(object sender, EventArgs e)
	{
		try
		{
			if (_browserSessionBridge == null)
			{
				throw new InvalidOperationException("Мост расширения браузера недоступен.");
			}
			BrowserSessionBridge.PairingInfo pairing = _browserSessionBridge.BeginPairing();
			System.Windows.Clipboard.SetText(pairing.Code);
			string extensionPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "PwHubBrowserExtension"));
			System.Windows.MessageBox.Show(
				$"Выбранный аккаунт: {pairing.AccountName}\n\nКод подключения: {pairing.Code}\n\nКод скопирован в буфер обмена и действует 5 минут. Откройте расширение «Обмен сессиями PW Hub», вставьте код и нажмите «Подключить».\n\nПапка расширения:\n{extensionPath}",
				"Подключение расширения браузера",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(
				ex.Message,
				"Подключение расширения браузера",
				MessageBoxButton.OK,
				MessageBoxImage.Warning);
		}
	}

	private void App_OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		IReadOnlyList<string> startupLogs = StartupDiagnostics.WriteFailure("Диспетчер интерфейса", e.Exception);
		try
		{
			Log.For<App>().Error("Unhandled UI exception", e.Exception);
		}
		catch
		{
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Исключение: " + e.Exception.Message);
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("Стек вызовов: " + (e.Exception.StackTrace ?? "пусто"));
			File.WriteAllText("error.log", stringBuilder.ToString());
		}
		catch
		{
		}
		string logMessage = startupLogs.Count > 0
			? "\n\nДиагностический лог:\n" + string.Join("\n", startupLogs)
			: string.Empty;
		System.Windows.MessageBox.Show(
			"В PW Account Hub произошла непредвиденная ошибка.\n\n" + e.Exception + logMessage,
			"PW Account Hub",
			MessageBoxButton.OK,
			MessageBoxImage.Error);
	}

	protected override void OnExit(ExitEventArgs e)
	{
		try
		{
			_browserSessionBridge?.Dispose();
		}
		catch
		{
		}
		try
		{
			NotifyIcon?.Dispose();
		}
		catch
		{
		}
		try
		{
			_singleInstanceMutex?.ReleaseMutex();
		}
		catch
		{
		}
		Log.Shutdown();
		base.OnExit(e);
	}

	private static void ActivateExistingInstance()
	{
		try
		{
			Process current = Process.GetCurrentProcess();
			List<Process> list = (from p in Process.GetProcessesByName(current.ProcessName)
				where p.Id != current.Id
				orderby p.StartTime
				select p).ToList();
			if (list.Count == 0)
			{
				return;
			}
			Process target = list[0];
			nint hwnd = IntPtr.Zero;
			EnumWindows(delegate(nint h, nint l)
			{
				GetWindowThreadProcessId(h, out var lpdwProcessId);
				if (lpdwProcessId == (uint)target.Id && IsWindowVisible(h))
				{
					hwnd = h;
					return false;
				}
				return true;
			}, IntPtr.Zero);
			if (hwnd != IntPtr.Zero)
			{
				if (IsIconic(hwnd))
				{
					ShowWindowAsync(hwnd, 9);
				}
				SetForegroundWindow(hwnd);
			}
		}
		catch
		{
		}
	}

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindowAsync(nint hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(nint hWnd);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			base.DispatcherUnhandledException += App_OnDispatcherUnhandledException;
			Uri resourceLocator = new Uri("/Pw.Hub;component/app.xaml", UriKind.Relative);
			System.Windows.Application.LoadComponent(this, resourceLocator);
		}
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public static void Main()
	{
		try
		{
			App app = new App();
			app.InitializeComponent();
			app.Run();
		}
		catch (Exception ex)
		{
			Environment.ExitCode = 1;
			IReadOnlyList<string> startupLogs = StartupDiagnostics.WriteFailure("Запуск приложения", ex);
			string logMessage = startupLogs.Count > 0
				? "\n\nДиагностический лог:\n" + string.Join("\n", startupLogs)
				: string.Empty;
			System.Windows.MessageBox.Show(
				"Не удалось запустить PW Account Hub.\n\n" + ex.Message + logMessage,
				"PW Account Hub",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
		}
	}
}

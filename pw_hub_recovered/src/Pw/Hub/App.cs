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
using Pw.Hub.Services;
using Pw.Hub.ViewModels;
using Pw.Hub.Windows;

namespace Pw.Hub;

public class App : System.Windows.Application
{
	private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	public NotifyIconEx.NotifyIcon NotifyIcon;

	private AuthService _authService;

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
		NotifyIcon.AddMenu("Check authorization", OnClick);
		NotifyIcon.AddMenu("Check for updates", delegate
		{
			System.Windows.MessageBox.Show(
				"Updates are managed by Game Tools Hub.",
				"PW Account Hub",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
		});
		NotifyIcon.AddMenu("-");
		NotifyIcon.AddMenu("Exit", delegate
		{
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

	private void App_OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		IReadOnlyList<string> startupLogs = StartupDiagnostics.WriteFailure("UI dispatcher", e.Exception);
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
			stringBuilder.AppendLine("Exception: " + e.Exception.Message);
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("Stack trace: " + (e.Exception.StackTrace ?? "empty"));
			File.WriteAllText("error.log", stringBuilder.ToString());
		}
		catch
		{
		}
		string logMessage = startupLogs.Count > 0
			? "\n\nDiagnostic log:\n" + string.Join("\n", startupLogs)
			: string.Empty;
		System.Windows.MessageBox.Show(
			"PW Account Hub encountered an unexpected error.\n\n" + e.Exception + logMessage,
			"PW Account Hub",
			MessageBoxButton.OK,
			MessageBoxImage.Error);
	}

	protected override void OnExit(ExitEventArgs e)
	{
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
			IReadOnlyList<string> startupLogs = StartupDiagnostics.WriteFailure("Application bootstrap", ex);
			string logMessage = startupLogs.Count > 0
				? "\n\nDiagnostic log:\n" + string.Join("\n", startupLogs)
				: string.Empty;
			System.Windows.MessageBox.Show(
				"PW Account Hub could not start.\n\n" + ex.Message + logMessage,
				"PW Account Hub",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
		}
	}
}

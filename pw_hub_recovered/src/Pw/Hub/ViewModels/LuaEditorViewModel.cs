using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Pw.Hub.Infrastructure;
using Pw.Hub.Infrastructure.Logging;
using Pw.Hub.Services;
using Pw.Hub.Tools;
using Pw.Hub.Windows;

namespace Pw.Hub.ViewModels;

public class LuaEditorViewModel : BaseViewModel
{
	private readonly ILuaDebugService _debugService;

	private readonly IUiDialogService _dialogs;

	private LuaScriptRunner _runner;

	private ModuleArgsWindow _runArgsWindow;

	private Window _ownerWindow;

	private string _code = string.Empty;

	private bool _isRunning;

	private string _logText = string.Empty;

	public LuaEditorAiViewModel Ai { get; } = new LuaEditorAiViewModel();

	public ObservableCollection<InputDefinitionDto> Inputs { get; } = new ObservableCollection<InputDefinitionDto>();

	public string Code
	{
		get
		{
			return _code;
		}
		set
		{
			_code = value ?? string.Empty;
			OnPropertyChanged("Code");
			OnPropertyChanged("CanRun");
			try
			{
				CommandManager.InvalidateRequerySuggested();
			}
			catch
			{
			}
		}
	}

	internal string LastCodeSentToAi { get; set; } = string.Empty;

	public bool IsRunning
	{
		get
		{
			return _isRunning;
		}
		private set
		{
			_isRunning = value;
			OnPropertyChanged("IsRunning");
			CommandManager.InvalidateRequerySuggested();
		}
	}

	public bool CanRun
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Code) && _runner != null)
			{
				return !IsRunning;
			}
			return false;
		}
	}

	public ObservableCollection<int> Breakpoints { get; } = new ObservableCollection<int>();

	public string LogText
	{
		get
		{
			return _logText;
		}
		set
		{
			_logText = value ?? string.Empty;
			OnPropertyChanged("LogText");
		}
	}

	public ICommand RunCommand { get; }

	public ICommand DebugCommand { get; }

	public ICommand StopCommand { get; }

	public ICommand ToggleBreakpointCommand { get; }

	public ICommand ClearLogCommand { get; }

	public event Action<int, IDictionary<string, object>, IDictionary<string, object>> RequestOpenDebugVariables;

	public LuaEditorViewModel()
		: this(new LuaDebugService(), new UiDialogService())
	{
	}

	public LuaEditorViewModel(ILuaDebugService debugService, IUiDialogService dialogs)
	{
		_debugService = debugService ?? new LuaDebugService();
		_dialogs = dialogs ?? new UiDialogService();
		RunCommand = new RelayCommand(delegate
		{
			Run();
		}, (object _) => CanRun);
		DebugCommand = new RelayCommand(delegate
		{
			Debug();
		}, (object _) => CanRun && Breakpoints.Count > 0);
		StopCommand = new RelayCommand(delegate
		{
			Stop();
		}, (object _) => IsRunning);
		ToggleBreakpointCommand = new RelayCommand(delegate(object p)
		{
			ToggleBreakpoint(p);
		}, (object _) => !IsRunning);
		ClearLogCommand = new RelayCommand(delegate
		{
			LogText = string.Empty;
		}, (object _) => !IsRunning);
		Ai.GetCurrentCode = () => Code;
		Ai.ApplyCode = delegate(string code)
		{
			Code = (code ?? string.Empty).Replace("\r\n", "\n");
		};
		Ai.ParentVm = this;
	}

	public void SetRunner(LuaScriptRunner runner)
	{
		_runner = runner;
		try
		{
			OnPropertyChanged("CanRun");
		}
		catch
		{
		}
		try
		{
			CommandManager.InvalidateRequerySuggested();
		}
		catch
		{
		}
	}

	public void SetOwner(Window owner)
	{
		_ownerWindow = owner;
	}

	private void ToggleBreakpoint(object parameter)
	{
		try
		{
			int result;
			int num = ((parameter is int num2) ? num2 : ((!(parameter is string s) || !int.TryParse(s, out result)) ? (-1) : result));
			int num3 = num;
			if (num3 > 0)
			{
				if (Breakpoints.Contains(num3))
				{
					Breakpoints.Remove(num3);
				}
				else
				{
					Breakpoints.Add(num3);
				}
			}
		}
		catch
		{
		}
		finally
		{
			CommandManager.InvalidateRequerySuggested();
		}
	}

	private void Stop()
	{
		try
		{
			_runner?.Stop();
			AppendLog("[Остановлено пользователем]");
		}
		catch
		{
		}
		finally
		{
			IsRunning = false;
		}
	}

	private async void Run()
	{
		if (!CanRun)
		{
			return;
		}
		Dictionary<string, object> args = new Dictionary<string, object>();
		try
		{
			if (Inputs != null && Inputs.Count > 0)
			{
				Dictionary<string, object> dictionary = _dialogs.AskRunArguments(Inputs.ToList(), ref _runArgsWindow, _ownerWindow);
				if (dictionary == null)
				{
					return;
				}
				args = dictionary;
			}
		}
		catch
		{
		}
		IsRunning = true;
		Guid runId = Guid.Empty;
		ILogger logger = Log.For<LuaEditorViewModel>();
		try
		{
			runId = RunContextTracker.BeginRun();
			try
			{
				_runner.SetRunId(runId);
			}
			catch
			{
			}
			_runner.SetPrintSink(AppendLog);
			_runner.SetProgressSink(delegate
			{
			});
			await _runner.RunCodeAsync(Code, args);
		}
		catch (Exception ex)
		{
			_dialogs.Alert("Ошибка запуска: " + ex.Message, "Lua");
			AppendLog("[Ошибка] " + ex);
		}
		finally
		{
			try
			{
				await RunContextTracker.EndRunCloseAll((Application.Current?.MainWindow as MainWindow)?.BrowserManager, logger, runId);
				RunContextTracker.ClearActive();
			}
			catch
			{
			}
			IsRunning = false;
		}
	}

	private async void Debug()
	{
		if (!CanRun || Breakpoints.Count == 0)
		{
			return;
		}
		Dictionary<string, object> args = new Dictionary<string, object>();
		try
		{
			if (Inputs != null && Inputs.Count > 0)
			{
				Dictionary<string, object> dictionary = _dialogs.AskRunArguments(Inputs.ToList(), ref _runArgsWindow, _ownerWindow);
				if (dictionary == null)
				{
					return;
				}
				args = dictionary;
			}
		}
		catch
		{
		}
		IsRunning = true;
		Guid runId = Guid.Empty;
		ILogger logger = Log.For<LuaEditorViewModel>();
		try
		{
			runId = RunContextTracker.BeginRun();
			try
			{
				_runner.SetRunId(runId);
			}
			catch
			{
			}
			LuaScriptRunner.DebugBreakHandler onBreak = delegate(int line, IDictionary<string, object> locals, IDictionary<string, object> globals)
			{
				try
				{
					this.RequestOpenDebugVariables?.Invoke(line, locals, globals);
				}
				catch
				{
				}
				return true;
			};
			await _debugService.RunWithBreakpointsAsync(_runner, Code, new HashSet<int>(Breakpoints), onBreak, args);
		}
		catch (Exception ex)
		{
			_dialogs.Alert("Ошибка отладки: " + ex.Message, "Lua");
			AppendLog("[Ошибка] " + ex);
		}
		finally
		{
			try
			{
				await RunContextTracker.EndRunCloseAll((Application.Current?.MainWindow as MainWindow)?.BrowserManager, logger, runId);
				RunContextTracker.ClearActive();
			}
			catch
			{
			}
			IsRunning = false;
		}
	}

	private void AppendLog(string text)
	{
		if (!string.IsNullOrEmpty(text))
		{
			if (!string.IsNullOrEmpty(LogText))
			{
				LogText += Environment.NewLine;
			}
			LogText += text;
			OnPropertyChanged("LogText");
		}
	}
}

using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using Pw.Hub.Infrastructure;

namespace Pw.Hub.ViewModels;

public class ScriptLogViewModel : BaseViewModel
{
	private readonly Stopwatch _stopwatch = new Stopwatch();

	private readonly DispatcherTimer _timer;

	private string _title = "\ud83d\udcdd Логи выполнения";

	private bool _isRunning = true;

	private string _statusText = string.Empty;

	private int _percent;

	private string _elapsedText = "⏱ Текущее время выполнения: 00:00";

	private readonly StringBuilder _logBuilder = new StringBuilder();

	private string _logText = string.Empty;

	private Action _onStop;

	public string Title
	{
		get
		{
			return _title;
		}
		set
		{
			_title = value;
			OnPropertyChanged("Title");
		}
	}

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
			OnPropertyChanged("CanClose");
		}
	}

	public bool CanClose => !IsRunning;

	public string StatusText
	{
		get
		{
			return _statusText;
		}
		private set
		{
			_statusText = value;
			OnPropertyChanged("StatusText");
		}
	}

	public int Percent
	{
		get
		{
			return _percent;
		}
		private set
		{
			_percent = value;
			OnPropertyChanged("Percent");
			OnPropertyChanged("PercentText");
			OnPropertyChanged("ProgressValue");
		}
	}

	public string PercentText => Percent + "%";

	public int ProgressValue => Percent;

	public string ElapsedText
	{
		get
		{
			return _elapsedText;
		}
		private set
		{
			_elapsedText = value;
			OnPropertyChanged("ElapsedText");
		}
	}

	public string LogText
	{
		get
		{
			return _logText;
		}
		private set
		{
			_logText = value;
			OnPropertyChanged("LogText");
			this.RequestScrollToEnd?.Invoke();
		}
	}

	public ICommand StopCommand { get; }

	public ICommand CloseCommand { get; }

	public event Action RequestScrollToEnd;

	public event Action RequestClose;

	public ScriptLogViewModel()
	{
		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_timer.Tick += delegate
		{
			UpdateElapsed();
		};
		_stopwatch.Start();
		_timer.Start();
		UpdateElapsed();
		StopCommand = new RelayCommand(delegate
		{
			_onStop?.Invoke();
		}, (object _) => IsRunning);
		CloseCommand = new RelayCommand(delegate
		{
			if (CanClose)
			{
				this.RequestClose?.Invoke();
			}
		}, (object _) => CanClose);
	}

	public void SetStopAction(Action onStop)
	{
		_onStop = onStop;
	}

	public void SetRunning(bool running)
	{
		IsRunning = running;
		if (!running)
		{
			if (_stopwatch.IsRunning)
			{
				_stopwatch.Stop();
			}
			_timer.Stop();
			UpdateElapsed(final: true);
		}
		else
		{
			if (!_stopwatch.IsRunning)
			{
				_stopwatch.Start();
			}
			_timer.Start();
		}
		CommandManager.InvalidateRequerySuggested();
	}

	public void AppendLog(string line)
	{
		if (!string.IsNullOrEmpty(line))
		{
			if (_logBuilder.Length > 0)
			{
				_logBuilder.AppendLine();
			}
			_logBuilder.Append(line);
			LogText = _logBuilder.ToString();
		}
	}

	public void ReportProgress(int percent, string message = null)
	{
		if (percent < 0)
		{
			percent = 0;
		}
		if (percent > 100)
		{
			percent = 100;
		}
		Percent = percent;
		StatusText = message ?? string.Empty;
	}

	public void MarkCompleted(string finalMessage = null)
	{
		SetRunning(running: false);
		ReportProgress(100, "Готово");
		if (_logBuilder.Length > 0)
		{
			_logBuilder.AppendLine();
		}
		StringBuilder logBuilder = _logBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(18, 1, logBuilder);
		handler.AppendLiteral("Время выполнения: ");
		handler.AppendFormatted(FormatElapsed(_stopwatch.Elapsed));
		logBuilder.AppendLine(ref handler);
		if (!string.IsNullOrWhiteSpace(finalMessage))
		{
			_logBuilder.AppendLine();
			_logBuilder.AppendLine("=== Результат ===");
			_logBuilder.AppendLine(finalMessage);
		}
		LogText = _logBuilder.ToString();
	}

	private void UpdateElapsed(bool final = false)
	{
		string text = (final ? "⏱ Итоговое время выполнения: " : "⏱ Текущее время выполнения: ");
		ElapsedText = text + FormatElapsed(_stopwatch.Elapsed);
	}

	private static string FormatElapsed(TimeSpan ts)
	{
		if (!(ts.TotalHours >= 1.0))
		{
			return $"{ts.Minutes:00}:{ts.Seconds:00}";
		}
		return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
	}
}

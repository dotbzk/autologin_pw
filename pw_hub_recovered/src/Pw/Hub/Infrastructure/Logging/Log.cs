using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Pw.Hub.Infrastructure.Logging;

public static class Log
{
	private sealed class CategoryLogger(string category) : ILogger
	{
		private readonly string _category = category;

		public void Log(LogLevel level, string message, int? eventId = null, Exception? ex = null, IDictionary<string, object?>? ctx = null)
		{
			if (level >= _config.MinimumLevel)
			{
				Enqueue(new LogEntry
				{
					TimestampUtc = DateTime.UtcNow,
					Level = level,
					Category = category,
					Message = message,
					EventId = eventId,
					Exception = ex?.ToString(),
					ThreadId = Environment.CurrentManagedThreadId,
					Context = ((ctx != null) ? new Dictionary<string, object>(ctx) : new Dictionary<string, object>())
				});
			}
		}

		public void Trace(string message, IDictionary<string, object?>? ctx = null)
		{
			Log(LogLevel.Trace, message, null, null, ctx);
		}

		public void Debug(string message, IDictionary<string, object?>? ctx = null)
		{
			Log(LogLevel.Debug, message, null, null, ctx);
		}

		public void Info(string message, IDictionary<string, object?>? ctx = null)
		{
			Log(LogLevel.Information, message, null, null, ctx);
		}

		public void Warn(string message, IDictionary<string, object?>? ctx = null)
		{
			Log(LogLevel.Warning, message, null, null, ctx);
		}

		public void Error(string message, Exception? ex = null, IDictionary<string, object?>? ctx = null)
		{
			Log(LogLevel.Error, message, null, ex, ctx);
		}

		public void Critical(string message, Exception? ex = null, IDictionary<string, object?>? ctx = null)
		{
			Log(LogLevel.Critical, message, null, ex, ctx);
		}
	}

	private static readonly object _initLock = new object();

	private static volatile bool _initialized;

	private static LoggerConfig _config = new LoggerConfig();

	private static readonly ConcurrentQueue<LogEntry> _queue = new ConcurrentQueue<LogEntry>();

	private static int _queueCount;

	private static CancellationTokenSource? _cts;

	private static Task? _worker;

	private static readonly JsonSerializerOptions _json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = false
	};

	private static string _currentFilePath = string.Empty;

	private static DateOnly _currentDate;

	private static readonly object _fileLock = new object();

	public static event Action<LogEntry>? EntryPublished;

	public static void Initialize(LoggerConfig? config)
	{
		if (_initialized)
		{
			return;
		}
		lock (_initLock)
		{
			if (!_initialized)
			{
				_config = config ?? new LoggerConfig();
				try
				{
					Directory.CreateDirectory(_config.LogsDirectory);
				}
				catch
				{
				}
				_cts = new CancellationTokenSource();
				_worker = Task.Run(() => WorkerAsync(_cts.Token));
				_initialized = true;
			}
		}
	}

	public static void Shutdown()
	{
		try
		{
			_cts?.Cancel();
			_worker?.Wait(1000);
		}
		catch
		{
		}
	}

	public static ILogger For<T>()
	{
		return new CategoryLogger(typeof(T).FullName ?? typeof(T).Name);
	}

	public static ILogger For(string category)
	{
		return new CategoryLogger(category);
	}

	private static void Enqueue(LogEntry entry)
	{
		if (!_initialized)
		{
			Initialize(null);
		}
		if (Interlocked.Increment(ref _queueCount) > _config.MaxQueueSize)
		{
			Interlocked.Decrement(ref _queueCount);
			return;
		}
		_queue.Enqueue(entry);
		Log.EntryPublished?.Invoke(entry);
	}

	private static async Task WorkerAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				if (!_queue.TryDequeue(out var result))
				{
					await Task.Delay(20, ct);
					continue;
				}
				Interlocked.Decrement(ref _queueCount);
				WriteEntry(result);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch
			{
			}
		}
		LogEntry result2;
		while (_queue.TryDequeue(out result2))
		{
			try
			{
				WriteEntry(result2);
			}
			catch
			{
			}
		}
	}

	private static void WriteEntry(LogEntry e)
	{
		DateOnly dateOnly = DateOnly.FromDateTime(DateTime.UtcNow);
		if (dateOnly != _currentDate || string.IsNullOrEmpty(_currentFilePath))
		{
			lock (_fileLock)
			{
				_currentDate = dateOnly;
				_currentFilePath = Path.Combine(_config.LogsDirectory, $"{dateOnly:yyyy-MM-dd}.jsonl");
			}
		}
		string contents = JsonSerializer.Serialize(e, _json) + "\n";
		lock (_fileLock)
		{
			File.AppendAllText(_currentFilePath, contents);
		}
	}
}

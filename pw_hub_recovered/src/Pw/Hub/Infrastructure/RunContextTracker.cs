using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pw.Hub.Infrastructure.Logging;
using Pw.Hub.Services;

namespace Pw.Hub.Infrastructure;

public static class RunContextTracker
{
	private static readonly ILogger _log = Log.For("RunContextTracker");

	private static readonly AsyncLocal<Guid?> _currentRun = new AsyncLocal<Guid?>();

	private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<int, byte>> _byRun = new ConcurrentDictionary<Guid, ConcurrentDictionary<int, byte>>();

	public static bool AutoCloseV2OnFinish { get; set; } = true;

	public static Guid BeginRun()
	{
		Guid guid = Guid.NewGuid();
		_currentRun.Value = guid;
		_byRun.TryAdd(guid, new ConcurrentDictionary<int, byte>());
		try
		{
			_log.Debug("BeginRun", new Dictionary<string, object> { { "runId", guid } });
		}
		catch
		{
		}
		return guid;
	}

	public static void SetActive(Guid runId)
	{
		_currentRun.Value = runId;
	}

	public static void ClearActive()
	{
		_currentRun.Value = null;
	}

	public static void RegisterHandle(int handle)
	{
		Guid? value = _currentRun.Value;
		if (!value.HasValue || !_byRun.TryGetValue(value.Value, out var value2))
		{
			return;
		}
		value2.TryAdd(handle, 0);
		try
		{
			_log.Debug("RegisterHandle (implicit)", new Dictionary<string, object>
			{
				{ "runId", value },
				{ "handle", handle }
			});
		}
		catch
		{
		}
	}

	public static void RegisterHandle(int handle, Guid runId)
	{
		_byRun.GetOrAdd(runId, (Guid _) => new ConcurrentDictionary<int, byte>()).TryAdd(handle, 0);
		try
		{
			_log.Debug("RegisterHandle (explicit)", new Dictionary<string, object>
			{
				{ "runId", runId },
				{ "handle", handle }
			});
		}
		catch
		{
		}
	}

	public static void UnregisterHandle(int handle)
	{
		Guid? value = _currentRun.Value;
		if (!value.HasValue || !_byRun.TryGetValue(value.Value, out var value2))
		{
			return;
		}
		value2.TryRemove(handle, out var _);
		try
		{
			_log.Debug("UnregisterHandle (implicit)", new Dictionary<string, object>
			{
				{ "runId", value },
				{ "handle", handle }
			});
		}
		catch
		{
		}
	}

	public static void UnregisterHandle(int handle, Guid runId)
	{
		if (_byRun.TryGetValue(runId, out var value))
		{
			value.TryRemove(handle, out var _);
			try
			{
				_log.Debug("UnregisterHandle (explicit)", new Dictionary<string, object>
				{
					{ "runId", runId },
					{ "handle", handle }
				});
			}
			catch
			{
			}
		}
	}

	public static int[] Snapshot(Guid runId)
	{
		if (_byRun.TryGetValue(runId, out var value))
		{
			return value.Keys.ToArray();
		}
		return Array.Empty<int>();
	}

	public static async Task EndRunCloseAll(BrowserManager? mgr, ILogger? log, Guid runId)
	{
		try
		{
			if (log == null)
			{
				log = _log;
			}
		}
		catch
		{
		}
		ConcurrentDictionary<int, byte> value;
		if (!AutoCloseV2OnFinish)
		{
			try
			{
				log?.Info("EndRun: auto-close disabled");
			}
			catch
			{
			}
			_byRun.TryRemove(runId, out value);
			return;
		}
		int[] array = Snapshot(runId);
		int num = 0;
		try
		{
			log?.Info("EndRun: cleanup start", new Dictionary<string, object>
			{
				{ "runId", runId },
				{ "handles", array }
			});
		}
		catch
		{
		}
		if (mgr != null)
		{
			int[] array2 = array;
			foreach (int handle in array2)
			{
				try
				{
					if (mgr.Close(handle))
					{
						num++;
					}
				}
				catch
				{
				}
			}
		}
		_byRun.TryRemove(runId, out value);
		try
		{
			log?.Info("EndRun: cleanup completed", new Dictionary<string, object>
			{
				{ "runId", runId },
				{ "closed", num },
				{ "total", array.Length }
			});
		}
		catch
		{
		}
		await Task.CompletedTask;
	}
}

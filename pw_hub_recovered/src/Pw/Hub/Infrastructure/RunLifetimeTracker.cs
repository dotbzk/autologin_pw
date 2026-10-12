using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Pw.Hub.Infrastructure.Logging;

namespace Pw.Hub.Infrastructure;

public static class RunLifetimeTracker
{
	private static readonly ILogger _log = Log.For("RunLifetimeTracker");

	private static readonly ConcurrentDictionary<Guid, int> _counts = new ConcurrentDictionary<Guid, int>();

	private static readonly AsyncLocal<Guid?> _current = new AsyncLocal<Guid?>();

	public static void SetActive(Guid runId)
	{
		_current.Value = runId;
	}

	public static void ClearActive()
	{
		_current.Value = null;
	}

	public static void BeginOp(Guid? runId = null)
	{
		Guid? guid = runId ?? _current.Value;
		if (!guid.HasValue)
		{
			return;
		}
		_counts.AddOrUpdate(guid.Value, 1, (Guid _, int old) => Math.Max(0, old) + 1);
		try
		{
			_log.Debug("BeginOp", new Dictionary<string, object>
			{
				{ "runId", guid },
				{
					"count",
					GetCount(guid.Value)
				}
			});
		}
		catch
		{
		}
	}

	public static void EndOp(Guid? runId = null)
	{
		Guid? guid = runId ?? _current.Value;
		if (!guid.HasValue)
		{
			return;
		}
		_counts.AddOrUpdate(guid.Value, 0, (Guid _, int old) => (old > 0) ? (old - 1) : 0);
		try
		{
			_log.Debug("EndOp", new Dictionary<string, object>
			{
				{ "runId", guid },
				{
					"count",
					GetCount(guid.Value)
				}
			});
		}
		catch
		{
		}
	}

	public static int GetCount(Guid runId)
	{
		if (!_counts.TryGetValue(runId, out var value))
		{
			return 0;
		}
		if (value >= 0)
		{
			return value;
		}
		return 0;
	}

	public static void Reset(Guid runId)
	{
		_counts.TryRemove(runId, out var _);
	}
}

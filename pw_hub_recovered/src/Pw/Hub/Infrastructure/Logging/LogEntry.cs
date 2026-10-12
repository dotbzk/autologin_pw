using System;
using System.Collections.Generic;

namespace Pw.Hub.Infrastructure.Logging;

public sealed class LogEntry
{
	public DateTime TimestampUtc { get; set; }

	public LogLevel Level { get; set; }

	public string Category { get; set; } = string.Empty;

	public string Message { get; set; } = string.Empty;

	public int? EventId { get; set; }

	public Dictionary<string, object?> Context { get; set; } = new Dictionary<string, object>();

	public string? Exception { get; set; }

	public int ThreadId { get; set; }
}

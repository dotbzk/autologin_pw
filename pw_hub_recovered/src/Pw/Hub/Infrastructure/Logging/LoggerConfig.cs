using System;
using System.IO;

namespace Pw.Hub.Infrastructure.Logging;

public sealed class LoggerConfig
{
	public string LogsDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "logs");

	public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

	public int MaxQueueSize { get; set; } = 5000;
}

using System;
using System.Collections.Generic;

namespace Pw.Hub.Infrastructure.Logging;

public interface ILogger
{
	void Log(LogLevel level, string message, int? eventId = null, Exception? ex = null, IDictionary<string, object?>? ctx = null);

	void Trace(string message, IDictionary<string, object?>? ctx = null);

	void Debug(string message, IDictionary<string, object?>? ctx = null);

	void Info(string message, IDictionary<string, object?>? ctx = null);

	void Warn(string message, IDictionary<string, object?>? ctx = null);

	void Error(string message, Exception? ex = null, IDictionary<string, object?>? ctx = null);

	void Critical(string message, Exception? ex = null, IDictionary<string, object?>? ctx = null);
}

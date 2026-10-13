using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Pw.Hub.Infrastructure.Logging;

public static class StartupDiagnostics
{
	private const string StartupLogName = "startup-error.log";

	public static string GetLogsDirectory()
	{
		string applicationDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
		if (CanWriteTo(applicationDirectory))
		{
			return applicationDirectory;
		}
		return GetFallbackLogsDirectory();
	}

	public static IReadOnlyList<string> WriteFailure(string stage, Exception exception)
	{
		List<string> writtenPaths = new List<string>();
		string entry = BuildEntry(stage, exception);
		foreach (string directory in CandidateLogsDirectories())
		{
			try
			{
				Directory.CreateDirectory(directory);
				string path = Path.Combine(directory, StartupLogName);
				File.AppendAllText(path, entry, Encoding.UTF8);
				writtenPaths.Add(path);
			}
			catch
			{
			}
		}
		return writtenPaths;
	}

	private static IEnumerable<string> CandidateLogsDirectories()
	{
		string applicationDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
		string fallbackDirectory = GetFallbackLogsDirectory();
		yield return applicationDirectory;
		if (!string.Equals(applicationDirectory, fallbackDirectory, StringComparison.OrdinalIgnoreCase))
		{
			yield return fallbackDirectory;
		}
	}

	private static string GetFallbackLogsDirectory()
	{
		string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		if (string.IsNullOrWhiteSpace(localApplicationData))
		{
			localApplicationData = Path.GetTempPath();
		}
		return Path.Combine(localApplicationData, "GameToolsHub", "PwHub", "logs");
	}

	private static bool CanWriteTo(string directory)
	{
		try
		{
			Directory.CreateDirectory(directory);
			string probe = Path.Combine(directory, $".write-test-{Environment.ProcessId}-{Guid.NewGuid():N}");
			File.WriteAllText(probe, string.Empty);
			File.Delete(probe);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string BuildEntry(string stage, Exception exception)
	{
		StringBuilder result = new StringBuilder();
		result.AppendLine($"[{DateTimeOffset.UtcNow:O}] PW Account Hub startup failure");
		result.AppendLine($"Stage: {stage}");
		result.AppendLine(exception.ToString());
		result.AppendLine();
		return result.ToString();
	}
}

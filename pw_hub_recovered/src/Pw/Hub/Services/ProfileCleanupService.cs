using System;
using System.IO;
using System.Threading.Tasks;

namespace Pw.Hub.Services;

public static class ProfileCleanupService
{
	public static Task CleanupLeftoversAsync(TimeSpan? olderThan = null)
	{
		return Task.Run(async delegate
		{
			try
			{
				TimeSpan age = olderThan ?? TimeSpan.FromHours(12.0);
				string profilesRoot = GetProfilesRoot();
				if (!string.IsNullOrWhiteSpace(profilesRoot) && Directory.Exists(profilesRoot))
				{
					foreach (string item in Directory.EnumerateDirectories(profilesRoot))
					{
						try
						{
							DateTime lastWriteTimeUtc = new DirectoryInfo(item).LastWriteTimeUtc;
							if (!(DateTime.UtcNow - lastWriteTimeUtc < age))
							{
								await TryDeleteDirWithRetries(item);
							}
						}
						catch
						{
						}
					}
				}
			}
			catch
			{
			}
		});
	}

	private static string GetProfilesRoot()
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PwHub", "WebViewProfiles");
			Directory.CreateDirectory(text);
			return text;
		}
		catch
		{
			try
			{
				string text2 = Path.Combine(Path.GetTempPath(), "PwHub", "WebViewProfiles");
				Directory.CreateDirectory(text2);
				return text2;
			}
			catch
			{
				return string.Empty;
			}
		}
	}

	private static async Task<bool> TryDeleteDirWithRetries(string path, int attempts = 10, int delayMs = 250)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}
		for (int i = 0; i < attempts; i++)
		{
			try
			{
				if (Directory.Exists(path))
				{
					try
					{
						foreach (string item in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
						{
							try
							{
								File.SetAttributes(item, FileAttributes.Normal);
							}
							catch
							{
							}
						}
					}
					catch
					{
					}
					Directory.Delete(path, recursive: true);
				}
				return true;
			}
			catch
			{
				try
				{
					await Task.Delay(delayMs);
				}
				catch
				{
				}
			}
		}
		return false;
	}
}

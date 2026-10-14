using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Pw.Hub.Windows;

namespace Pw.Hub.Services;

public class UpdateService
{
	private readonly string _manifestUrl;

	private readonly HttpClient _http = new HttpClient();

	public UpdateService()
	{
		string text = Environment.GetEnvironmentVariable("PW_MODULES_API")?.TrimEnd('/') ?? "https://api.pw-hub.ru";
		_manifestUrl = text + "/api/app/manifest";
	}

	public Version GetCurrentVersion()
	{
		Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
		string text = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		if (!string.IsNullOrWhiteSpace(text) && Version.TryParse(NormalizeSemVer(text), out Version result))
		{
			return result;
		}
		FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
		if (Version.TryParse(NormalizeSemVer(versionInfo.ProductVersion ?? versionInfo.FileVersion), out Version result2))
		{
			return result2;
		}
		return assembly.GetName().Version ?? new Version(1, 0, 0, 0);
	}

	public async Task CheckForUpdates(bool showNoUpdatesMessage = false)
	{
		UpdateManifest manifest;
		try
		{
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10.0));
			using HttpResponseMessage resp = await _http.GetAsync(_manifestUrl, cts.Token);
			resp.EnsureSuccessStatusCode();
			await using Stream stream = await resp.Content.ReadAsStreamAsync(cts.Token);
			manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			}, cts.Token);
		}
		catch (Exception ex)
		{
			if (showNoUpdatesMessage)
			{
				ShowMessage("Could not check for updates.\n" + ex.Message, "Update", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			return;
		}
		if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
		{
			if (showNoUpdatesMessage)
			{
				ShowMessage("The update manifest is invalid.", "Update", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			return;
		}
		Version currentVersion = GetCurrentVersion();
		if (!Version.TryParse(NormalizeSemVer(manifest.Version), out Version result))
		{
			if (showNoUpdatesMessage)
			{
				ShowMessage("Could not parse the version in the update manifest.", "Update", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			return;
		}
		if (result <= currentVersion)
		{
			if (showNoUpdatesMessage)
			{
				ShowMessage($"You already have the latest version ({currentVersion}).", "Update", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			return;
		}
		string value = (string.IsNullOrWhiteSpace(manifest.ReleaseNotes) ? string.Empty : ("\n\nChanges:\n" + manifest.ReleaseNotes));
		MessageBoxResult messageBoxResult = ShowMessage($"Version {result} is available. Current version: {currentVersion}.{value}\n\nDownload and install it now?", "Update Available", manifest.Mandatory ? MessageBoxButton.OKCancel : MessageBoxButton.YesNo, MessageBoxImage.Asterisk);
		if (!(manifest.Mandatory ? (messageBoxResult == MessageBoxResult.OK) : (messageBoxResult == MessageBoxResult.Yes)))
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(manifest.Url))
		{
			ShowMessage("The manifest does not contain a download URL.", "Update", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		Window owner = null;
		UpdateProgressWindow progress = null;
		Application.Current?.Dispatcher.Invoke(delegate
		{
			owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault((Window w) => w.IsActive && w.IsVisible) ?? Application.Current?.MainWindow;
			progress = new UpdateProgressWindow();
			if (owner != null)
			{
				progress.Owner = owner;
				owner.IsEnabled = false;
			}
			progress.SetTitle("Downloading update...");
			progress.SetStatus("Connecting...");
			progress.Show();
		});
		try
		{
			string tempDir = Path.Combine(Path.GetTempPath(), "PwHubUpdate");
			Directory.CreateDirectory(tempDir);
			string fileName = Path.GetFileName(new Uri(manifest.Url).LocalPath);
			if (string.IsNullOrWhiteSpace(fileName))
			{
				fileName = $"PwHubSetup_{result}.exe";
			}
			string target = Path.Combine(tempDir, fileName);
			using HttpResponseMessage resp = await _http.GetAsync(manifest.Url, HttpCompletionOption.ResponseHeadersRead);
			resp.EnsureSuccessStatusCode();
			long? contentLength = resp.Content.Headers.ContentLength;
			if (contentLength.HasValue && contentLength.Value > 0)
			{
				progress?.SetIndeterminate(indeterminate: false);
			}
			else
			{
				progress?.SetIndeterminate(indeterminate: true);
			}
			await using (Stream stream = await resp.Content.ReadAsStreamAsync())
			{
				await using FileStream fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
				byte[] buffer = new byte[81920];
				long totalRead = 0L;
				Stopwatch sw = Stopwatch.StartNew();
				while (true)
				{
					int num;
					int read = (num = await stream.ReadAsync(buffer, 0, buffer.Length));
					if (num <= 0)
					{
						break;
					}
					await fs.WriteAsync(buffer, 0, read);
					totalRead += read;
					if (contentLength.HasValue && contentLength.Value > 0)
					{
						double num2 = (double)totalRead / (double)contentLength.Value * 100.0;
						progress?.SetProgress(num2);
						if (sw.ElapsedMilliseconds > 250)
						{
							double value2 = (double)totalRead / 1024.0 / 1024.0;
							double value3 = (double)contentLength.Value / 1024.0 / 1024.0;
							progress?.SetStatus($"Downloading... {Math.Round(num2)}% ({value2:0.#} / {value3:0.#} MB)");
							sw.Restart();
						}
					}
					else if (sw.ElapsedMilliseconds > 300)
					{
						double value4 = (double)totalRead / 1024.0 / 1024.0;
						progress?.SetStatus($"Downloading... {value4:0.#} MB");
						sw.Restart();
					}
				}
			}
			string text = Path.GetExtension(target).ToLowerInvariant();
			string text2 = Process.GetCurrentProcess().MainModule?.FileName ?? Assembly.GetEntryAssembly()?.Location ?? string.Empty;
			string appDir = (string.IsNullOrWhiteSpace(text2) ? AppContext.BaseDirectory : Path.GetDirectoryName(text2));
			if (text == ".zip")
			{
				progress?.SetTitle("Installing update...");
				progress?.SetStatus("Extracting files...");
				progress?.SetIndeterminate(indeterminate: true);
				string text3 = Path.Combine(tempDir, "extracted");
				if (Directory.Exists(text3))
				{
					Directory.Delete(text3, recursive: true);
				}
				ZipFile.ExtractToDirectory(target, text3);
				progress?.SetStatus("Preparing to replace files...");
				CloseProgress();
				RunUpdaterScriptAndShutdown(text3, appDir, text2, replaceExeOnly: false);
				return;
			}
			string text4 = fileName.ToLowerInvariant();
			if (text == ".msi" || (text == ".exe" && (text4.Contains("setup") || text4.Contains("install") || text4.Contains("installer") || text4.Contains("setup-"))))
			{
				progress?.SetTitle("Installing update...");
				progress?.SetStatus("Starting installer...");
				CloseProgress();
				Process.Start(new ProcessStartInfo
				{
					FileName = target,
					UseShellExecute = true,
					Verb = "open"
				});
				Application.Current.Dispatcher.Invoke(delegate
				{
					Application.Current.Shutdown();
				});
				return;
			}
			progress?.SetTitle("Installing update...");
			progress?.SetStatus("Preparing installation...");
			CloseProgress();
			RunUpdaterScriptAndShutdown(target, appDir, text2, replaceExeOnly: true);
		}
		catch (Exception ex2)
		{
			CloseProgress();
			ShowMessage("Could not download or install the update.\n" + ex2.Message, "Update", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		void CloseProgress()
		{
			try
			{
				Application.Current?.Dispatcher.Invoke(delegate
				{
					progress?.CloseSafe();
					if (owner != null)
					{
						owner.IsEnabled = true;
					}
				});
			}
			catch
			{
			}
		}
	}

	private static void RunUpdaterScriptAndShutdown(string newPayloadPath, string appDir, string appExePath, bool replaceExeOnly)
	{
		try
		{
			string text = Path.Combine(Path.GetTempPath(), "PwHubUpdate");
			Directory.CreateDirectory(text);
			string text2 = Path.Combine(text, "update.ps1");
			int id = Process.GetCurrentProcess().Id;
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Param(\n  [Parameter(Mandatory=$true)][string]$NewPayloadPath,\n  [Parameter(Mandatory=$true)][string]$AppDir,\n  [Parameter(Mandatory=$true)][string]$AppExePath,\n  [Parameter(Mandatory=$true)][int]$ProcId,\n  [switch]$ReplaceExeOnly\n)");
			stringBuilder.AppendLine("$ErrorActionPreference = 'Stop'");
			stringBuilder.AppendLine("try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.UTF8Encoding]::new($false) } catch {} ");
			stringBuilder.AppendLine("$LogDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath 'PwHubUpdate'");
			stringBuilder.AppendLine("$LogPath = Join-Path -Path $LogDir -ChildPath 'update.log'");
			stringBuilder.AppendLine("New-Item -ItemType Directory -Force -Path $LogDir | Out-Null");
			stringBuilder.AppendLine("try { $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'; Add-Content -LiteralPath $LogPath -Value \"[$ts] Bootstrap: script started\" -Encoding UTF8 } catch {} ");
			stringBuilder.AppendLine("function Write-Log([string]$msg){ try { $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'; $line = \"[$ts] $msg\"; Write-Host $line; Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8 } catch {} }");
			stringBuilder.AppendLine("try { Start-Transcript -Path $LogPath -Append -Force | Out-Null } catch {} ");
			stringBuilder.AppendLine("Write-Log '== Update started =='");
			stringBuilder.AppendLine("Write-Log \"Parameters: NewPayloadPath=[$NewPayloadPath], AppDir=[$AppDir], AppExePath=[$AppExePath], ProcId=[$ProcId], ReplaceExeOnly=[$ReplaceExeOnly]\"");
			stringBuilder.AppendLine("function Ensure-Elevation {\n  try {\n    $testFile = Join-Path -Path $AppDir -ChildPath ('write_test_' + [guid]::NewGuid().ToString() + '.tmp')\n    Set-Content -LiteralPath $testFile -Value 'test' -Encoding UTF8\n    Remove-Item -LiteralPath $testFile -Force -ErrorAction SilentlyContinue\n    return $true\n  } catch {\n    return $false\n  }\n}");
			stringBuilder.AppendLine("if (-not (Ensure-Elevation)) {\n  Write-Log 'Administrator permissions are required. Requesting elevation...'\n  $args = @('-NoProfile','-ExecutionPolicy','Bypass','-File', $PSCommandPath, '-NewPayloadPath', $NewPayloadPath, '-AppDir', $AppDir, '-AppExePath', $AppExePath, '-ProcId', $ProcId.ToString())\n  if ($ReplaceExeOnly) { $args += '-ReplaceExeOnly' }\n  Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $args\n  exit 0\n}");
			stringBuilder.AppendLine("Write-Log 'Waiting for the application to exit...'");
			stringBuilder.AppendLine("try { $p = Get-Process -Id $ProcId -ErrorAction SilentlyContinue; while ($p) { Start-Sleep -Milliseconds 200; $p = Get-Process -Id $ProcId -ErrorAction SilentlyContinue } } catch { Write-Log ('Process wait error: ' + $_) }");
			stringBuilder.AppendLine("Start-Sleep -Milliseconds 200");
			stringBuilder.AppendLine("try {");
			stringBuilder.AppendLine("  if ($ReplaceExeOnly) {");
			stringBuilder.AppendLine("    Write-Log 'Replacing executable...' ");
			stringBuilder.AppendLine("    $attempts = 0; while ($true) { try { Copy-Item -LiteralPath $NewPayloadPath -Destination $AppExePath -Force; Write-Log 'EXE replaced.'; break } catch { $attempts++; Write-Log ('Copy attempt failed: ' + $_.Exception.Message); if ($attempts -ge 10) { throw }; Start-Sleep -Milliseconds 200 } } ");
			stringBuilder.AppendLine("  } else {");
			stringBuilder.AppendLine("    Write-Log 'Copying new files...' ");
			stringBuilder.AppendLine("    $items = Get-ChildItem -LiteralPath $NewPayloadPath -Force ");
			stringBuilder.AppendLine("    foreach ($it in $items) { try { Write-Log (\"Copying: \" + $it.FullName); Copy-Item -LiteralPath $it.FullName -Destination $AppDir -Recurse -Force } catch { Write-Log ('Copy error for ' + $it.FullName + ': ' + $_.Exception.Message); throw } }");
			stringBuilder.AppendLine("  }");
			stringBuilder.AppendLine("  try { Unblock-File -LiteralPath $AppExePath } catch { Write-Log ('Unblock-File error: ' + $_.Exception.Message) } ");
			stringBuilder.AppendLine("  Write-Log 'Starting application...'");
			stringBuilder.AppendLine("  Start-Process -FilePath $AppExePath -WorkingDirectory $AppDir | Out-Null");
			stringBuilder.AppendLine("  Write-Log 'Done. Exiting.'");
			stringBuilder.AppendLine("  try { Stop-Transcript | Out-Null } catch {} ");
			stringBuilder.AppendLine("  exit 0");
			stringBuilder.AppendLine("} catch { Write-Log ('FATAL ERROR: ' + $_.Exception.ToString()); try { Stop-Transcript | Out-Null } catch {}; Write-Host 'Press Enter to close this window...'; Read-Host | Out-Null; exit 1 }");
			File.WriteAllText(text2, stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			try
			{
				File.AppendAllText(Path.Combine(text, "prelauncher.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Preparing to start PowerShell.\r\nScriptPath={text2}\r\nNewPayloadPath={newPayloadPath}\r\nAppDir={appDir}\r\nAppExePath={appExePath}\r\nReplaceExeOnly={replaceExeOnly}\r\n", Encoding.UTF8);
			}
			catch
			{
			}
			string text3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32\\WindowsPowerShell\\v1.0\\powershell.exe");
			if (!File.Exists(text3))
			{
				text3 = "powershell.exe";
			}
			bool flag = false;
			Exception ex = null;
			try
			{
				flag = Process.Start(new ProcessStartInfo
				{
					FileName = text3,
					UseShellExecute = true,
					WindowStyle = ProcessWindowStyle.Normal,
					WorkingDirectory = text,
					Verb = "open",
					Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{text2}\" -NewPayloadPath \"{newPayloadPath}\" -AppDir \"{appDir}\" -AppExePath \"{appExePath}\" -ProcId {id}{(replaceExeOnly ? " -ReplaceExeOnly" : string.Empty)}"
				}) != null;
			}
			catch (Exception ex2)
			{
				ex = ex2;
			}
			if (!flag)
			{
				try
				{
					flag = Process.Start(new ProcessStartInfo
					{
						FileName = "cmd.exe",
						UseShellExecute = true,
						WindowStyle = ProcessWindowStyle.Normal,
						WorkingDirectory = text,
						Arguments = $"/c \"\"{text3}\" -NoProfile -ExecutionPolicy Bypass -File \"{text2}\" -NewPayloadPath \"{newPayloadPath}\" -AppDir \"{appDir}\" -AppExePath \"{appExePath}\" -ProcId {id}{(replaceExeOnly ? " -ReplaceExeOnly" : string.Empty)}\""
					}) != null;
				}
				catch (Exception ex3)
				{
					ex = ex3;
				}
			}
			if (!flag)
			{
			ShowMessage("Could not open the PowerShell update window.\\n" + ex?.Message, "Update", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
			Application.Current.Dispatcher.Invoke(delegate
			{
				Application.Current.Shutdown();
			});
		}
		catch (Exception ex4)
		{
			ShowMessage("Could not prepare update installation.\n" + ex4.Message, "Update", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private static MessageBoxResult ShowMessage(string text, string caption, MessageBoxButton buttons, MessageBoxImage icon)
	{
		try
		{
			Dispatcher dispatcher = Application.Current?.Dispatcher;
			if (dispatcher != null)
			{
				if (dispatcher.CheckAccess())
				{
					return ShowMessageCore(text, caption, buttons, icon);
				}
				return dispatcher.Invoke(() => ShowMessageCore(text, caption, buttons, icon));
			}
		}
		catch
		{
		}
		return MessageBox.Show(text, caption, buttons, icon);
	}

	private static MessageBoxResult ShowMessageCore(string text, string caption, MessageBoxButton buttons, MessageBoxImage icon)
	{
		Window window = Application.Current?.Windows.OfType<Window>().FirstOrDefault((Window w) => w.IsActive && w.IsVisible) ?? Application.Current?.MainWindow;
		if (window != null && window.IsVisible)
		{
			return MessageBox.Show(window, text, caption, buttons, icon);
		}
		return MessageBox.Show(text, caption, buttons, icon, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
	}

	private static string NormalizeSemVer(string version)
	{
		if (string.IsNullOrWhiteSpace(version))
		{
			return "0.0.0";
		}
		return version.Split('+')[0].Trim();
	}
}

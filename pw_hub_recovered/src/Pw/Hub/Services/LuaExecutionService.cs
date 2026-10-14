using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Pw.Hub.Infrastructure;
using Pw.Hub.Infrastructure.Logging;
using Pw.Hub.Models;
using Pw.Hub.Tools;
using Pw.Hub.Windows;

namespace Pw.Hub.Services;

public class LuaExecutionService : ILuaExecutionService
{
	public async Task RunAsync(LuaScriptRunner runner, ModuleDefinition module, Dictionary<string, object> args, Window owner)
	{
		if (runner == null)
		{
			throw new ArgumentNullException("runner");
		}
		if (module == null)
		{
			throw new ArgumentNullException("module");
		}
		if (owner == null)
		{
			owner = System.Windows.Application.Current?.MainWindow;
		}
		ScriptLogWindow logWindow = new ScriptLogWindow(module.Name)
		{
			Owner = owner
		};
		runner.SetPrintSink(delegate(string text)
		{
			logWindow.AppendLog(text);
		});
		runner.SetProgressSink(delegate(int percent, string message)
		{
			logWindow.ReportProgress(percent, message);
		});
		logWindow.SetStopAction(delegate
		{
			try
			{
				runner.Stop();
				logWindow.AppendLog("[Stopped by user]");
			}
			catch
			{
			}
		});
		logWindow.SetRunning(running: true);
		Guid runId = RunContextTracker.BeginRun();
		MainWindow main = (owner as MainWindow) ?? (System.Windows.Application.Current?.MainWindow as MainWindow);
		ILogger logger = Log.For<LuaExecutionService>();
		try
		{
			runner.SetRunId(runId);
		}
		catch
		{
		}
		string result = null;
		try
		{
			runner.RunModuleAsync(module, args).ContinueWith(delegate(Task<string> t)
			{
				try
				{
					result = (t.IsCompletedSuccessfully ? t.Result : null);
					logWindow.MarkCompleted(result);
					if (System.Windows.Application.Current is App app)
					{
						string tipTitle = "Module: " + module.Name;
						string tipText = (string.IsNullOrWhiteSpace(result) ? "Completed" : result);
						app.NotifyIcon?.ShowBalloonTip(5, tipTitle, tipText, ToolTipIcon.Info);
					}
				}
				catch
				{
				}
				finally
				{
					try
					{
						RunContextTracker.EndRunCloseAll(main?.BrowserManager, logger, runId);
					}
					catch
					{
					}
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
			logWindow.ShowDialog();
			await Task.Yield();
		}
		catch
		{
		}
		finally
		{
			try
			{
				await RunContextTracker.EndRunCloseAll(main?.BrowserManager, logger, runId);
			}
			catch
			{
			}
			try
			{
				RunContextTracker.ClearActive();
			}
			catch
			{
			}
		}
	}
}

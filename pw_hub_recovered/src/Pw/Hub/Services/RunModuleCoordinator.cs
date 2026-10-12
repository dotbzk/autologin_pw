using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Pw.Hub.Models;
using Pw.Hub.Tools;
using Pw.Hub.Windows;

namespace Pw.Hub.Services;

public sealed class RunModuleCoordinator : IRunModuleCoordinator
{
	private readonly IWindowService _windows;

	private readonly ILuaExecutionService _exec;

	private readonly ModuleService _moduleService;

	public RunModuleCoordinator(IWindowService windows, ILuaExecutionService exec)
	{
		_windows = windows ?? new WindowService();
		_exec = exec ?? new LuaExecutionService();
		_moduleService = new ModuleService();
	}

	public async Task RunAsync(ModuleDefinition module)
	{
		if (module == null)
		{
			return;
		}
		ModuleArgsWindow moduleArgsWindow = new ModuleArgsWindow(module);
		if (_windows.ShowDialog(moduleArgsWindow) != true)
		{
			return;
		}
		try
		{
			module.LastArgs = moduleArgsWindow.StringValues;
			_moduleService.AddOrUpdateModule(module);
		}
		catch
		{
		}
		try
		{
			Window window = Application.Current?.MainWindow;
			if (window is MainWindow mainWindow)
			{
				LuaScriptRunner runner = new LuaScriptRunner(mainWindow.AccountPage.AccountManager, mainWindow.AccountPage.Browser);
				IncrementRunIfApiModuleAsync(module);
				await _exec.RunAsync(runner, module, moduleArgsWindow.Values ?? new Dictionary<string, object>(), window);
			}
		}
		catch
		{
		}
	}

	private static async Task IncrementRunIfApiModuleAsync(ModuleDefinition module)
	{
		try
		{
			if (Guid.TryParse(module.Id, out var result))
			{
				await new ModulesApiClient().IncrementRunAsync(result);
			}
		}
		catch
		{
		}
	}
}

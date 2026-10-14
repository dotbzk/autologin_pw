using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using NLua;
using Pw.Hub.Abstractions;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;

namespace Pw.Hub.Tools;

public class LuaScriptRunner
{
	public delegate bool DebugBreakHandler(int line, IDictionary<string, object> locals, IDictionary<string, object> globals);

	private class DebugBridge
	{
		private readonly DebugBreakHandler _onBreak;

		public DebugBridge(DebugBreakHandler onBreak)
		{
			_onBreak = onBreak;
		}

		public void OnBreak(object lineObj, object localsObj, object globalsObj)
		{
			try
			{
				int result = 0;
				if (lineObj is double num)
				{
					result = (int)num;
				}
				else if (lineObj is int num2)
				{
					result = num2;
				}
				else if (lineObj != null)
				{
					int.TryParse(lineObj.ToString(), out result);
				}
				HashSet<object> visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
				IDictionary<string, object> locals = ConvertTable(localsObj, 7, 0, visited);
				HashSet<object> visited2 = new HashSet<object>(ReferenceEqualityComparer.Instance);
				IDictionary<string, object> globals = ConvertTable(globalsObj, 6, 0, visited2);
				_onBreak?.Invoke(result, locals, globals);
			}
			catch
			{
			}
		}
	}

	private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
	{
		public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

		public new bool Equals(object x, object y)
		{
			return x == y;
		}

		public int GetHashCode(object obj)
		{
			return RuntimeHelpers.GetHashCode(obj);
		}
	}

	private class LuaCompleteBridge
	{
		private readonly TaskCompletionSource<string> _tcs;

		public LuaCompleteBridge(TaskCompletionSource<string> tcs)
		{
			_tcs = tcs;
		}

		public void Complete(object value)
		{
			try
			{
				_tcs.TrySetResult(value?.ToString());
			}
			catch
			{
				_tcs.TrySetResult(null);
			}
		}
	}

	private readonly IAccountManager _accountManager;

	private readonly IBrowser _browser;

	private Lua _currentLua;

	private TaskCompletionSource<string> _currentTcs;

	private readonly LuaIntegration _integration;

	private Guid? _runId;

	public void SetRunId(Guid runId)
	{
		_runId = runId;
		try
		{
			_integration.SetRunId(runId);
		}
		catch
		{
		}
		try
		{
			RunLifetimeTracker.SetActive(runId);
		}
		catch
		{
		}
	}

	public LuaScriptRunner(IAccountManager accountManager, IBrowser browser)
	{
		_accountManager = accountManager;
		_browser = browser;
		_integration = new LuaIntegration(_accountManager, _browser);
	}

	public void SetPrintSink(Action<string> sink)
	{
		_integration.SetPrintSink(sink);
	}

	public void SetProgressSink(Action<int, string> sink)
	{
		_integration.SetProgressSink(sink);
	}

	public async Task RunAsync(string scriptFileName)
	{
		_ = 1;
		try
		{
			string text = Path.Combine(AppContext.BaseDirectory, "Scripts", scriptFileName);
			if (!File.Exists(text))
			{
				MessageBox.Show("Скрипт " + scriptFileName + " не найден по пути: " + text, "Lua", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			else
			{
				await RunCodeAsync(await File.ReadAllTextAsync(text, Encoding.UTF8));
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Ошибка во время выполнения Lua-скрипта: " + ex.Message, "Lua", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	public Task RunCodeAsync(string code)
	{
		return RunCodeAsync(code, null);
	}

	public Task RunCodeAsync(string code, Dictionary<string, object> args)
	{
		_currentLua?.Dispose();
		_currentLua = new Lua();
		_currentLua.State.Encoding = Encoding.UTF8;
		_integration.Register(_currentLua);
		if (args != null)
		{
			try
			{
				_currentLua.NewTable("args");
				LuaTable luaTable = (LuaTable)_currentLua["args"];
				foreach (KeyValuePair<string, object> arg in args)
				{
					try
					{
						object value = _integration.ConvertToLuaValue(arg.Value);
						luaTable[arg.Key] = value;
					}
					catch
					{
						luaTable[arg.Key] = arg.Value;
					}
				}
			}
			catch
			{
			}
		}
		_currentTcs = new TaskCompletionSource<string>();
		LuaCompleteBridge target = new LuaCompleteBridge(_currentTcs);
		MethodInfo method = typeof(LuaCompleteBridge).GetMethod("Complete", new Type[1] { typeof(object) });
		if (method != null)
		{
			_currentLua.RegisterFunction("Complete", target, method);
		}
		return Task.Run(delegate
		{
			try
			{
				_currentLua.DoString(code);
				Guid? runId = _runId;
				if (runId.HasValue && _currentTcs != null)
				{
					Task.Run(async delegate
					{
						try
						{
							int graceMs = 400;
							DateTime quietSince = DateTime.UtcNow;
							int lastCount = -1;
							while (true)
							{
								int count = RunLifetimeTracker.GetCount(runId.Value);
								if (count == 0)
								{
									if ((DateTime.UtcNow - quietSince).TotalMilliseconds >= (double)graceMs)
									{
										break;
									}
								}
								else
								{
									quietSince = DateTime.UtcNow;
								}
								if (count != lastCount)
								{
									lastCount = count;
									if (count != 0)
									{
										quietSince = DateTime.UtcNow;
									}
								}
								await Task.Delay(50).ConfigureAwait(continueOnCapturedContext: false);
								if (_currentTcs == null)
								{
									return;
								}
							}
							_currentTcs?.TrySetResult(null);
						}
						catch
						{
						}
					});
				}
			}
			catch (ObjectDisposedException)
			{
				_currentTcs?.TrySetResult(null);
			}
			catch (Exception ex2)
			{
				MessageBox.Show("Ошибка Lua: " + ex2.Message, "Lua", MessageBoxButton.OK, MessageBoxImage.Hand);
				_currentTcs?.TrySetResult(null);
			}
		}).ContinueWith((Task t) => _currentTcs.Task).Unwrap();
	}

	public Task RunCodeWithBreakpointsAsync(string code, IEnumerable<int> breakpoints, DebugBreakHandler onBreak, string selectedAccountId = null)
	{
		return RunCodeWithBreakpointsAsync(code, breakpoints, onBreak, null, selectedAccountId);
	}

	public Task RunCodeWithBreakpointsAsync(string code, IEnumerable<int> breakpoints, DebugBreakHandler onBreak, Dictionary<string, object> args, string selectedAccountId = null)
	{
		try
		{
			_currentLua?.Dispose();
			_currentLua = new Lua();
			_currentLua.State.Encoding = Encoding.UTF8;
			_integration.Register(_currentLua);
			if (args != null)
			{
				try
				{
					_currentLua.NewTable("args");
					LuaTable luaTable = (LuaTable)_currentLua["args"];
					foreach (KeyValuePair<string, object> arg in args)
					{
						try
						{
							object value = _integration.ConvertToLuaValue(arg.Value);
							luaTable[arg.Key] = value;
						}
						catch
						{
							luaTable[arg.Key] = arg.Value;
						}
					}
				}
				catch
				{
				}
			}
			if (!string.IsNullOrWhiteSpace(selectedAccountId))
			{
				_currentLua["selectedAccountId"] = selectedAccountId;
			}
			else
			{
				_currentLua["selectedAccountId"] = null;
			}
			_currentLua.NewTable("__pw_breakpoints");
			LuaTable luaTable2 = (LuaTable)_currentLua["__pw_breakpoints"];
			foreach (int item in breakpoints?.Distinct() ?? Enumerable.Empty<int>())
			{
				luaTable2[item] = true;
			}
			DebugBridge target = new DebugBridge(onBreak);
			MethodInfo method = typeof(DebugBridge).GetMethod("OnBreak");
			_currentLua.RegisterFunction("__pw_onbreak", target, method);
			_currentTcs = new TaskCompletionSource<string>();
			MethodInfo method2 = typeof(LuaCompleteBridge).GetMethod("Complete", new Type[1] { typeof(object) });
			if (method2 != null)
			{
				_currentLua.RegisterFunction("Complete", new LuaCompleteBridge(_currentTcs), method2);
			}
			string chunk = "\r\n-- Snapshot current globals to distinguish user-defined ones later\r\n__pw_initial_globals = {}\r\nfor k, _ in pairs(_G) do __pw_initial_globals[k] = true end\r\n\r\n-- Snapshot of top-level (chunk) locals captured during initial script run\r\n__pw_chunk_locals = {}\r\n\r\nlocal function __pw_collect_locals(level)\r\n  local t = {}\r\n  local i = 1\r\n  while true do\r\n    local name, value = debug.getlocal(level+1, i)\r\n    if not name then break end\r\n    if name ~= '(*temporary)' then\r\n      t[name] = value\r\n    end\r\n    i = i + 1\r\n  end\r\n  return t\r\nend\r\n\r\nlocal function __pw_collect_upvalues(level)\r\n  local t = {}\r\n  local info = debug.getinfo(level+1, 'f')\r\n  if info and info.func then\r\n    local i = 1\r\n    while true do\r\n      local name, value = debug.getupvalue(info.func, i)\r\n      if not name then break end\r\n      if name ~= '(*temporary)' then\r\n        t[name] = value\r\n      end\r\n      i = i + 1\r\n    end\r\n  end\r\n  return t\r\nend\r\n\r\nlocal function __pw_merge(a, b)\r\n  local t = {}\r\n  for k, v in pairs(a) do t[k] = v end\r\n  for k, v in pairs(b) do if t[k] == nil then t[k] = v end end\r\n  return t\r\nend\r\n\r\nlocal function __pw_collect_user_globals()\r\n  local t = {}\r\n  for k, v in pairs(_G) do\r\n    if not __pw_initial_globals[k] then\r\n      t[k] = v\r\n    end\r\n  end\r\n  return t\r\nend\r\n\r\nlocal function __pw_hook(event, line)\r\n  -- Continuously snapshot locals of the main chunk while it is running\r\n  do\r\n    local info = debug.getinfo(2, 'S')\r\n    if info and info.what == 'main' then\r\n      local i = 1\r\n      while true do\r\n        local name, value = debug.getlocal(2, i)\r\n        if not name then break end\r\n        if name ~= '(*temporary)' then __pw_chunk_locals[name] = value end\r\n        i = i + 1\r\n      end\r\n    end\r\n  end\r\n\r\n  if event == 'line' and __pw_breakpoints[line] then\r\n    -- level mapping: [1]=__pw_hook, [2]=running chunk/function (caller of hook)\r\n    local locals = __pw_collect_locals(2)\r\n    local ups = __pw_collect_upvalues(2)\r\n    local merged = __pw_merge(locals, ups)\r\n    local userg = __pw_merge(__pw_collect_user_globals(), __pw_chunk_locals)\r\n    __pw_onbreak(line, merged, userg)\r\n  end\r\nend\r\n\r\ndebug.sethook(__pw_hook, 'l')\r\n";
			_currentLua.DoString(chunk);
			_currentLua.DoString(code);
		}
		catch (ObjectDisposedException)
		{
			_currentTcs?.TrySetResult(null);
		}
		catch (Exception ex2)
		{
			MessageBox.Show("Ошибка во время отладки Lua-скрипта: " + ex2.Message, "Lua Debug", MessageBoxButton.OK, MessageBoxImage.Hand);
			_currentTcs?.TrySetResult(null);
		}
		return _currentTcs?.Task ?? Task.CompletedTask;
	}

	private static object ConvertValue(object val, int maxDepth, int currentDepth, ISet<object> visited)
	{
		try
		{
			if (val == null)
			{
				return null;
			}
			Type type = val.GetType();
			if (type.IsPrimitive || val is string || val is decimal || val is DateTime || val is Guid)
			{
				return val;
			}
			if (currentDepth >= maxDepth)
			{
				if (val is LuaTable)
				{
					return new Dictionary<string, object>();
				}
				return val?.ToString();
			}
			if (val is LuaTable tableObj)
			{
				return ConvertTable(tableObj, maxDepth, currentDepth, visited);
			}
			if (visited != null && !IsSimple(val))
			{
				if (visited.Contains(val))
				{
					return "<circular>";
				}
				visited.Add(val);
			}
			if (val is IEnumerable enumerable && !(val is string))
			{
				Dictionary<string, object> dictionary = new Dictionary<string, object>();
				int num = 1;
				foreach (object item in enumerable)
				{
					dictionary[num.ToString()] = ConvertValue(item, maxDepth, currentDepth + 1, visited);
					num++;
					if (num > 200)
					{
						break;
					}
				}
				dictionary["Count"] = num - 1;
				return dictionary;
			}
			Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
			dictionary2["__type"] = type.Name;
			PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
			foreach (PropertyInfo propertyInfo in properties)
			{
				if (propertyInfo.CanRead)
				{
					object val2;
					try
					{
						val2 = propertyInfo.GetValue(val, null);
					}
					catch
					{
						val2 = null;
					}
					dictionary2[propertyInfo.Name] = ConvertValue(val2, maxDepth, currentDepth + 1, visited);
				}
			}
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
			foreach (FieldInfo fieldInfo in fields)
			{
				object val3;
				try
				{
					val3 = fieldInfo.GetValue(val);
				}
				catch
				{
					val3 = null;
				}
				dictionary2[fieldInfo.Name] = ConvertValue(val3, maxDepth, currentDepth + 1, visited);
			}
			return dictionary2;
		}
		catch
		{
			return val;
		}
	}

	private static bool IsSimple(object val)
	{
		if (val != null && !val.GetType().IsPrimitive && !(val is string) && !(val is decimal) && !(val is DateTime))
		{
			return val is Guid;
		}
		return true;
	}

	private static IDictionary<string, object> ConvertTable(object tableObj, int maxDepth = 2, int currentDepth = 0, ISet<object> visited = null)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		try
		{
			if (tableObj is LuaTable luaTable)
			{
				if (visited != null)
				{
					if (visited.Contains(luaTable))
					{
						return dictionary;
					}
					visited.Add(luaTable);
				}
				if (currentDepth >= maxDepth)
				{
					return dictionary;
				}
				IDictionaryEnumerator enumerator = luaTable.GetEnumerator();
				int num = 0;
				while (enumerator.MoveNext() && num++ <= 200)
				{
					object key = enumerator.Key;
					object value = enumerator.Value;
					string key2;
					try
					{
						key2 = key?.ToString() ?? "<nil>";
					}
					catch
					{
						key2 = "<key>";
					}
					dictionary[key2] = ConvertValue(value, maxDepth, currentDepth + 1, visited);
				}
			}
		}
		catch
		{
		}
		return dictionary;
	}

	public async Task<string> RunModuleAsync(ModuleDefinition module, Dictionary<string, object> args)
	{
		try
		{
			string baseDirectory = AppContext.BaseDirectory;
			string text = module.Script;
			if (!Path.IsPathRooted(text))
			{
				string text2 = Path.Combine(baseDirectory, text);
				string text3 = Path.Combine(baseDirectory, "Scripts", text);
				text = ((!File.Exists(text2)) ? text3 : text2);
			}
			if (!File.Exists(text))
			{
				MessageBox.Show("Скрипт модуля не найден: " + module.Script, "Модули", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return null;
			}
			_currentLua = new Lua();
			_currentLua.State.Encoding = Encoding.UTF8;
			_integration.Register(_currentLua);
			_currentLua.NewTable("args");
			LuaTable luaTable = (LuaTable)_currentLua["args"];
			foreach (KeyValuePair<string, object> arg in args)
			{
				try
				{
					object value = _integration.ConvertToLuaValue(arg.Value);
					luaTable[arg.Key] = value;
				}
				catch
				{
					luaTable[arg.Key] = arg.Value;
				}
			}
			_currentTcs = new TaskCompletionSource<string>();
			LuaCompleteBridge target = new LuaCompleteBridge(_currentTcs);
			MethodInfo method = typeof(LuaCompleteBridge).GetMethod("Complete", new Type[1] { typeof(object) });
			if (method != null)
			{
				_currentLua.RegisterFunction("Complete", target, method);
			}
			string code = await File.ReadAllTextAsync(text, Encoding.UTF8);
			try
			{
				object[] array = await Task.Run(() => _currentLua.DoString(code));
				if (array != null && array.Length != 0 && array[0] != null)
				{
					return array[0]?.ToString();
				}
			}
			catch (ObjectDisposedException)
			{
				return null;
			}
			catch (Exception ex2)
			{
				MessageBox.Show("Ошибка при выполнении модуля: " + ex2.Message, "Модули", MessageBoxButton.OK, MessageBoxImage.Hand);
				return null;
			}
			return await _currentTcs.Task.ConfigureAwait(continueOnCapturedContext: true);
		}
		finally
		{
			try
			{
				_currentLua?.Dispose();
			}
			catch
			{
			}
			_currentLua = null;
			_currentTcs = null;
		}
	}

	public void Stop()
	{
		try
		{
			_currentTcs?.TrySetResult(null);
			try
			{
				if (_runId.HasValue)
				{
					RunLifetimeTracker.Reset(_runId.Value);
				}
			}
			catch
			{
			}
			try
			{
				_integration?.DisposeWorker();
			}
			catch
			{
			}
			_currentLua?.Dispose();
		}
		catch
		{
		}
	}
}

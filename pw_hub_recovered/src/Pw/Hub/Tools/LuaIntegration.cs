using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NLua;
using Pw.Hub.Abstractions;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Services;

namespace Pw.Hub.Tools;

public class LuaIntegration
{
	private sealed class LuaCallbackDispatcher : IDisposable
	{
		private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>(new ConcurrentQueue<Action>());

		private readonly Thread _thread;

		private volatile bool _stopping;

		public LuaCallbackDispatcher(string name = "LuaWorker")
		{
			_thread = new Thread(ThreadProc)
			{
				IsBackground = true,
				Name = name
			};
			_thread.Start();
		}

		private void ThreadProc()
		{
			try
			{
				foreach (Action item in _queue.GetConsumingEnumerable())
				{
					try
					{
						item();
					}
					catch
					{
					}
					if (_stopping && _queue.Count == 0)
					{
						break;
					}
				}
			}
			catch
			{
			}
		}

		public void Post(Action act)
		{
			if (!_queue.IsAddingCompleted)
			{
				try
				{
					_queue.Add(act);
				}
				catch
				{
				}
			}
		}

		public void Dispose()
		{
			try
			{
				_stopping = true;
				_queue.CompleteAdding();
				if (!_thread.Join(500))
				{
					try
					{
						_thread.Interrupt();
						return;
					}
					catch
					{
						return;
					}
				}
			}
			catch
			{
			}
		}
	}

	private LuaCallbackDispatcher _luaWorker;

	private readonly IAccountManager _accountManager;

	private readonly IBrowser _browser;

	private readonly HttpClient _client = new HttpClient();

	private readonly TaskCompletionSource<string> _tcs;

	private Lua _lua;

	private Action<string> _printSink;

	private Action<int, string> _progressSink;

	private Guid? _runId;

	public void SetRunId(Guid runId)
	{
		_runId = runId;
		try
		{
			RunContextTracker.SetActive(runId);
		}
		catch
		{
		}
		try
		{
			if (_luaWorker == null)
			{
				_luaWorker = new LuaCallbackDispatcher("LuaWorker-" + runId.ToString().Substring(0, 8));
			}
		}
		catch
		{
		}
	}

	public void DisposeWorker()
	{
		try
		{
			_luaWorker?.Dispose();
		}
		catch
		{
		}
		_luaWorker = null;
	}

	public LuaIntegration(IAccountManager accountManager)
	{
		_accountManager = accountManager;
		_browser = null;
	}

	public LuaIntegration(IAccountManager accountManager, IBrowser browser)
	{
		_accountManager = accountManager;
		_browser = browser;
	}

	public LuaIntegration(IAccountManager accountManager, TaskCompletionSource<string> tcs)
	{
		_accountManager = accountManager;
		_tcs = tcs;
		_browser = null;
	}

	public void Register(Lua lua)
	{
		_lua = lua;
		List<LuaApiRegistry.Entry> entries = new List<LuaApiRegistry.Entry>();
		lua.RegisterFunction("Account_GetAccountCb", this, GetType().GetMethod("Account_GetAccountCb", new Type[1] { typeof(LuaFunction) }));
		Add("Account_GetAccountCb", "v1", "Account", "Account_GetAccountCb(function(acc) ... end)", "Get the current account in callback(acc)", "Account_GetAccountCb(function(acc)\n    __CURSOR__\nend)");
		lua.RegisterFunction("Account_IsAuthorizedCb", this, GetType().GetMethod("Account_IsAuthorizedCb", new Type[1] { typeof(LuaFunction) }));
		Add("Account_IsAuthorizedCb", "v1", "Account", "Account_IsAuthorizedCb(function(isAuth) ... end)", "Check account authorization", "Account_IsAuthorizedCb(function(isAuth)\n    __CURSOR__\nend)");
		lua.RegisterFunction("Account_GetAccountsCb", this, GetType().GetMethod("Account_GetAccountsCb", new Type[1] { typeof(LuaFunction) }));
		Add("Account_GetAccountsCb", "v1", "Account", "Account_GetAccountsCb(function(accounts) ... end)", "Account list (table)", "Account_GetAccountsCb(function(accounts)\n    __CURSOR__\nend)");
		lua.RegisterFunction("Account_ChangeAccountCb", this, GetType().GetMethod("Account_ChangeAccountCb", new Type[2]
		{
			typeof(string),
			typeof(LuaFunction)
		}));
		Add("Account_ChangeAccountCb", "v1", "Account", "Account_ChangeAccountCb(accountId, function(ok) ... end)", "Switch the active account", "Account_ChangeAccountCb(accountId, function(ok)\n    __CURSOR__\nend)");
		if (_browser != null)
		{
			lua.RegisterFunction("Browser_NavigateCb", this, GetType().GetMethod("Browser_NavigateCb", new Type[2]
			{
				typeof(string),
				typeof(LuaFunction)
			}));
			Add("Browser_NavigateCb", "v1", "Browser", "Browser_NavigateCb(url, function() ... end)", "Open a URL", "Browser_NavigateCb(url, function()\n    __CURSOR__\nend)");
			lua.RegisterFunction("Browser_ReloadCb", this, GetType().GetMethod("Browser_ReloadCb", new Type[1] { typeof(LuaFunction) }));
			Add("Browser_ReloadCb", "v1", "Browser", "Browser_ReloadCb(function() ... end)", "Reload the page", "Browser_ReloadCb(function()\n    __CURSOR__\nend)");
			lua.RegisterFunction("Browser_ExecuteScriptCb", this, GetType().GetMethod("Browser_ExecuteScriptCb", new Type[2]
			{
				typeof(string),
				typeof(LuaFunction)
			}));
			Add("Browser_ExecuteScriptCb", "v1", "Browser", "Browser_ExecuteScriptCb(jsCode, function(result) ... end)", "Execute JavaScript and return the result", "Browser_ExecuteScriptCb(jsCode, function(result)\n    __CURSOR__\nend)");
			lua.RegisterFunction("Browser_ElementExistsCb", this, GetType().GetMethod("Browser_ElementExistsCb", new Type[2]
			{
				typeof(string),
				typeof(LuaFunction)
			}));
			Add("Browser_ElementExistsCb", "v1", "Browser", "Browser_ElementExistsCb(selector, function(exists) ... end)", "Check whether an element exists", "Browser_ElementExistsCb(selector, function(exists)\n    __CURSOR__\nend)");
			lua.RegisterFunction("Browser_WaitForElementCb", this, GetType().GetMethod("Browser_WaitForElementCb", new Type[3]
			{
				typeof(string),
				typeof(int),
				typeof(LuaFunction)
			}));
			Add("Browser_WaitForElementCb", "v1", "Browser", "Browser_WaitForElementCb(selector, timeoutMs, function(found) ... end)", "Wait for an element", "Browser_WaitForElementCb(selector, timeoutMs, function(found)\n    __CURSOR__\nend)");
		}
		try
		{
			lua.RegisterFunction("BrowserV2_Create", this, GetType().GetMethod("BrowserV2_Create", new Type[2]
			{
				typeof(object),
				typeof(LuaFunction)
			}));
			Add("BrowserV2_Create", "v2", "BrowserV2", "BrowserV2_Create(options, function(handle) ... end)", "Create a browser and return its handle", "BrowserV2_Create({}, function(handle)\n    __CURSOR__\nend)");
			lua.RegisterFunction("BrowserV2_Close", this, GetType().GetMethod("BrowserV2_Close", new Type[2]
			{
				typeof(int),
				typeof(LuaFunction)
			}));
			Add("BrowserV2_Close", "v2", "BrowserV2", "BrowserV2_Close(handle, function(ok) ... end)", "Close a created browser", "BrowserV2_Close(handle, function(ok)\n    __CURSOR__\nend)");
			lua.RegisterFunction("BrowserV2_Navigate", this, GetType().GetMethod("BrowserV2_Navigate", new Type[3]
			{
				typeof(int),
				typeof(string),
				typeof(LuaFunction)
			}));
			Add("BrowserV2_Navigate", "v2", "BrowserV2", "BrowserV2_Navigate(handle, url, function(ok) ... end)", "Open a URL in the specified browser", "BrowserV2_Navigate(handle, url, function(ok)\n    __CURSOR__\nend)");
			lua.RegisterFunction("BrowserV2_Reload", this, GetType().GetMethod("BrowserV2_Reload", new Type[2]
			{
				typeof(int),
				typeof(LuaFunction)
			}));
			Add("BrowserV2_Reload", "v2", "BrowserV2", "BrowserV2_Reload(handle, function(ok) ... end)", "Reload the page", "BrowserV2_Reload(handle, function(ok)\n    __CURSOR__\nend)");
			lua.RegisterFunction("BrowserV2_ExecuteScript", this, GetType().GetMethod("BrowserV2_ExecuteScript", new Type[3]
			{
				typeof(int),
				typeof(string),
				typeof(LuaFunction)
			}));
			Add("BrowserV2_ExecuteScript", "v2", "BrowserV2", "BrowserV2_ExecuteScript(handle, jsCode, function(result) ... end)", "Execute JavaScript in a v2 browser", "BrowserV2_ExecuteScript(handle, jsCode, function(result)\n    __CURSOR__\nend)");
			lua.RegisterFunction("BrowserV2_ElementExists", this, GetType().GetMethod("BrowserV2_ElementExists", new Type[3]
			{
				typeof(int),
				typeof(string),
				typeof(LuaFunction)
			}));
			Add("BrowserV2_ElementExists", "v2", "BrowserV2", "BrowserV2_ElementExists(handle, selector, function(exists) ... end)", "Check whether an element exists", "BrowserV2_ElementExists(handle, selector, function(exists)\n    __CURSOR__\nend)");
			lua.RegisterFunction("BrowserV2_WaitForElement", this, GetType().GetMethod("BrowserV2_WaitForElement", new Type[4]
			{
				typeof(int),
				typeof(string),
				typeof(int),
				typeof(LuaFunction)
			}));
			Add("BrowserV2_WaitForElement", "v2", "BrowserV2", "BrowserV2_WaitForElement(handle, selector, timeoutMs, function(found) ... end)", "Wait for an element", "BrowserV2_WaitForElement(handle, selector, timeoutMs, function(found)\n    __CURSOR__\nend)");
			lua.RegisterFunction("BrowserV2_ChangeAccount", this, GetType().GetMethod("BrowserV2_ChangeAccount", new Type[3]
			{
				typeof(int),
				typeof(string),
				typeof(LuaFunction)
			}));
			Add("BrowserV2_ChangeAccount", "v2", "BrowserV2", "BrowserV2_ChangeAccount(handle, accountId, function(ok) ... end)", "Switch the account in the specified v2 browser", "BrowserV2_ChangeAccount(handle, accountId, function(ok)\n    __CURSOR__\nend)");
			lua.RegisterFunction("BrowserV2_GetCurrentAccount", this, GetType().GetMethod("BrowserV2_GetCurrentAccount", new Type[2]
			{
				typeof(int),
				typeof(LuaFunction)
			}));
			Add("BrowserV2_GetCurrentAccount", "v2", "BrowserV2", "BrowserV2_GetCurrentAccount(handle, function(acc) ... end)", "Return the current account as a Lua table", "BrowserV2_GetCurrentAccount(handle, function(acc)\n    __CURSOR__\nend)");
		}
		catch
		{
		}
		lua.RegisterFunction("Print", this, GetType().GetMethod("Print"));
		Add("Print", "v1", "Helpers", "Print(value)", "Write text to the editor log", "Print('text')");
		lua.RegisterFunction("DelayCb", this, GetType().GetMethod("DelayCb", new Type[2]
		{
			typeof(int),
			typeof(LuaFunction)
		}));
		Add("DelayCb", "v1", "Helpers", "DelayCb(ms, function() ... end)", "Delay with a callback", "DelayCb(1000, function()\n    __CURSOR__\nend)");
		lua.RegisterFunction("ReportProgress", this, GetType().GetMethod("ReportProgress", new Type[1] { typeof(int) }));
		Add("ReportProgress", "v1", "Helpers", "ReportProgress(percent)", "Update execution progress", "ReportProgress(50)");
		lua.RegisterFunction("ReportProgressMsg", this, GetType().GetMethod("ReportProgressMsg", new Type[2]
		{
			typeof(int),
			typeof(string)
		}));
		Add("ReportProgressMsg", "v1", "Helpers", "ReportProgressMsg(percent, message)", "Update progress with a message", "ReportProgressMsg(25, 'Start')");
		lua.RegisterFunction("Net_PostJsonCb", this, GetType().GetMethod("NetPostJsonSb", new Type[4]
		{
			typeof(string),
			typeof(string),
			typeof(string),
			typeof(LuaFunction)
		}));
		Add("Net_PostJsonCb", "v1", "Net", "Net_PostJsonCb(url, jsonBody, contentType, function(res) ... end)", "Send an HTTP POST with JSON and return the response in res", "Net_PostJsonCb(url, json, 'application/json', function(res)\n    __CURSOR__\nend)");
		lua.RegisterFunction("Telegram_SendMessageCb", this, GetType().GetMethod("Telegram_SendMessageCb", new Type[2]
		{
			typeof(string),
			typeof(LuaFunction)
		}));
		Add("Telegram_SendMessageCb", "v1", "Telegram", "Telegram_SendMessageCb(text, function(ok) ... end)", "Send a Telegram message", "Telegram_SendMessageCb('text', function(ok)\n    __CURSOR__\nend)");
		try
		{
			LuaApiRegistry.ReplaceAll(entries);
		}
		catch
		{
		}
		void Add(string name, string version, string category, string signature, string description, string snippet)
		{
			entries.Add(new LuaApiRegistry.Entry
			{
				Name = name,
				Version = version,
				Category = category,
				Signature = signature,
				Description = description,
				Snippet = snippet
			});
		}
	}

	public void SetPrintSink(Action<string> sink)
	{
		_printSink = sink;
	}

	public void SetProgressSink(Action<int, string> sink)
	{
		_progressSink = sink;
	}

	[LuaApiFunction(Name = "Print", Version = "v1", Category = "Helpers", Signature = "Print(value)", Description = "Write text to the editor log", Snippet = "Print('text')")]
	public void Print(string text)
	{
		try
		{
			if (_printSink != null)
			{
				Application current = Application.Current;
				if (current?.Dispatcher != null)
				{
					current.Dispatcher.BeginInvoke((Action)delegate
					{
						try
						{
							_printSink?.Invoke(text);
						}
						catch
						{
						}
					});
					return;
				}
				try
				{
					_printSink?.Invoke(text);
					return;
				}
				catch
				{
					return;
				}
			}
		}
		catch
		{
		}
		MessageBox.Show("[Lua] " + text);
	}

	[LuaApiFunction(Name = "DelayCb", Version = "v1", Category = "Helpers", Signature = "DelayCb(ms, function() ... end)", Description = "Delay with a callback", Snippet = "DelayCb(1000, function()\n    __CURSOR__\nend)")]
	public void DelayCb(int ms, LuaFunction callback)
	{
		try
		{
			RunLifetimeTracker.BeginOp(_runId);
			Task.Delay(ms).ContinueWith(delegate
			{
				try
				{
					try
					{
						CallLuaVoid(callback);
					}
					finally
					{
						RunLifetimeTracker.EndOp(_runId);
					}
				}
				catch
				{
					try
					{
						RunLifetimeTracker.EndOp(_runId);
					}
					catch
					{
					}
				}
			});
		}
		catch
		{
			try
			{
				RunLifetimeTracker.EndOp(_runId);
			}
			catch
			{
			}
		}
	}

	[LuaApiFunction(Name = "ReportProgress", Version = "v1", Category = "Helpers", Signature = "ReportProgress(percent)", Description = "Update execution progress", Snippet = "ReportProgress(50)")]
	public void ReportProgress(int percent)
	{
		try
		{
			Action<int, string> sink = _progressSink;
			if (sink == null)
			{
				return;
			}
			Application current = Application.Current;
			if (current?.Dispatcher != null)
			{
				current.Dispatcher.BeginInvoke((Action)delegate
				{
					try
					{
						sink(percent, null);
					}
					catch
					{
					}
				});
				return;
			}
			try
			{
				sink(percent, null);
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	[LuaApiFunction(Name = "ReportProgressMsg", Version = "v1", Category = "Helpers", Signature = "ReportProgressMsg(percent, message)", Description = "Update progress with a message", Snippet = "ReportProgressMsg(25, 'Start')")]
	public void ReportProgressMsg(int percent, string message)
	{
		try
		{
			Action<int, string> sink = _progressSink;
			if (sink == null)
			{
				return;
			}
			Application current = Application.Current;
			if (current?.Dispatcher != null)
			{
				current.Dispatcher.BeginInvoke((Action)delegate
				{
					try
					{
						sink(percent, message);
					}
					catch
					{
					}
				});
				return;
			}
			try
			{
				sink(percent, message);
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private static void UiInvoke(Action action)
	{
		try
		{
			Application current = Application.Current;
			if (current?.Dispatcher != null)
			{
				current.Dispatcher.Invoke(delegate
				{
					try
					{
						action();
					}
					catch
					{
					}
				});
				return;
			}
			try
			{
				action();
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private static T UiInvoke<T>(Func<T> func)
	{
		try
		{
			Application current = Application.Current;
			if (current?.Dispatcher != null)
			{
				return current.Dispatcher.Invoke(delegate
				{
					try
					{
						return func();
					}
					catch
					{
						return default(T);
					}
				});
			}
			try
			{
				return func();
			}
			catch
			{
				return default(T);
			}
		}
		catch
		{
			return default(T);
		}
	}

	private void CallLuaVoid(LuaFunction callback, params object[] args)
	{
		if (callback == null)
		{
			return;
		}
		try
		{
			LuaCallbackDispatcher luaWorker = _luaWorker;
			if (luaWorker != null)
			{
				luaWorker.Post(delegate
				{
					try
					{
						callback.Call(args);
					}
					catch
					{
					}
				});
				return;
			}
			try
			{
				callback.Call(args);
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private object CallLuaWithReturn(LuaFunction callback, params object[] args)
	{
		if (callback == null)
		{
			return null;
		}
		object result = null;
		ManualResetEventSlim ev = new ManualResetEventSlim(initialState: false);
		try
		{
			LuaCallbackDispatcher luaWorker = _luaWorker;
			if (luaWorker != null)
			{
				luaWorker.Post(delegate
				{
					try
					{
						object[] array2 = callback.Call(args);
						result = ((array2 != null && array2.Length != 0) ? array2[0] : null);
					}
					catch
					{
					}
					finally
					{
						try
						{
							ev.Set();
						}
						catch
						{
						}
					}
				});
				ev.Wait(5000);
				return result;
			}
			try
			{
				object[] array = callback.Call(args);
				return (array != null && array.Length != 0) ? array[0] : null;
			}
			catch
			{
				return null;
			}
		}
		catch
		{
			return null;
		}
		finally
		{
			try
			{
				ev.Dispose();
			}
			catch
			{
			}
		}
	}

	public object ConvertToLuaValue(object value)
	{
		try
		{
			if (value == null)
			{
				return null;
			}
			if (value is Squad s)
			{
				return ToLuaSquad(s);
			}
			if (value is Account acc)
			{
				return ToLuaAccount(acc, includeSquad: true);
			}
			if (value is IEnumerable enumerable && !(value is string))
			{
				LuaTable luaTable = CreateLuaTable("list");
				int num = 1;
				foreach (object item in enumerable)
				{
					object value2 = ((item is Squad s2) ? ToLuaSquad(s2) : ((!(item is Account acc2)) ? item : ToLuaAccount(acc2, includeSquad: true)));
					luaTable[num++] = value2;
				}
				return luaTable;
			}
			return value;
		}
		catch
		{
			return null;
		}
	}

	[LuaApiFunction(Name = "Account_GetAccountCb", Version = "v1", Category = "Account", Signature = "Account_GetAccountCb(function(acc) ... end)", Description = "Get the current account in callback(acc)", Snippet = "Account_GetAccountCb(function(acc)\n    __CURSOR__\nend)")]
	public void Account_GetAccountCb(LuaFunction callback)
	{
		RunLifetimeTracker.BeginOp(_runId);
		_accountManager.GetAccountAsync().ContinueWith(delegate(Task<Account> t)
		{
			try
			{
				Account account = (t.IsCompletedSuccessfully ? t.Result : null);
				CallLuaVoid(callback, account);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "Account_IsAuthorizedCb", Version = "v1", Category = "Account", Signature = "Account_IsAuthorizedCb(function(isAuth) ... end)", Description = "Check account authorization", Snippet = "Account_IsAuthorizedCb(function(isAuth)\n    __CURSOR__\nend)")]
	public void Account_IsAuthorizedCb(LuaFunction callback)
	{
		RunLifetimeTracker.BeginOp(_runId);
		_accountManager.IsAuthorizedAsync().ContinueWith(delegate(Task<bool> t)
		{
			try
			{
				bool flag = t.IsCompletedSuccessfully && t.Result;
				CallLuaVoid(callback, flag);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "Account_GetAccountsCb", Version = "v1", Category = "Account", Signature = "Account_GetAccountsCb(function(accounts) ... end)", Description = "Account list (table)", Snippet = "Account_GetAccountsCb(function(accounts)\n    __CURSOR__\nend)")]
	public void Account_GetAccountsCb(LuaFunction callback)
	{
		RunLifetimeTracker.BeginOp(_runId);
		_accountManager.GetAccountsAsync().ContinueWith(delegate(Task<Account[]> t)
		{
			try
			{
				Account[] array = (t.IsCompletedSuccessfully ? t.Result : Array.Empty<Account>());
				if (_lua != null)
				{
					LuaTable luaTable = CreateLuaTable("accounts");
					int num = 1;
					Account[] array2 = array;
					foreach (Account acc in array2)
					{
						luaTable[num++] = ToLuaAccount(acc, includeSquad: true);
					}
					CallLuaVoid(callback, luaTable);
				}
				else
				{
					CallLuaVoid(callback, new object[1] { array });
				}
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	private LuaTable CreateLuaTable(string prefix)
	{
		return UiInvoke(delegate
		{
			if (_lua == null)
			{
				return (LuaTable)null;
			}
			string fullPath = $"__{prefix}_{Guid.NewGuid():N}";
			_lua.NewTable(fullPath);
			LuaTable result = (LuaTable)_lua[fullPath];
			try
			{
				_lua[fullPath] = null;
			}
			catch
			{
			}
			return result;
		});
	}

	private LuaTable ToLuaAccount(Account acc)
	{
		return ToLuaAccount(acc, includeSquad: false);
	}

	private LuaTable ToLuaAccount(Account acc, bool includeSquad)
	{
		LuaTable luaTable = CreateLuaTable("account");
		try
		{
			luaTable["Id"] = acc?.Id;
			luaTable["Name"] = acc?.Name;
			luaTable["SiteId"] = acc?.SiteId;
			luaTable["ImageSource"] = acc?.ImageSource;
			luaTable["LastVisit"] = acc?.LastVisit.ToString("o");
			luaTable["ImageUri"] = acc?.ImageUri?.ToString();
			luaTable["SquadId"] = acc?.SquadId;
			luaTable["OrderIndex"] = acc?.OrderIndex;
			if (includeSquad)
			{
				LuaTable luaTable2 = CreateLuaTable("squad");
				try
				{
					if (acc?.Squad != null)
					{
						luaTable2["Id"] = acc.Squad.Id;
						luaTable2["Name"] = acc.Squad.Name;
						luaTable2["OrderIndex"] = acc.Squad.OrderIndex;
					}
					else
					{
						luaTable2["Id"] = acc?.SquadId;
					}
				}
				catch
				{
				}
				luaTable["Squad"] = luaTable2;
			}
			LuaTable luaTable3 = CreateLuaTable("servers");
			int num = 1;
			if (acc?.Servers != null)
			{
				foreach (AccountServer server in acc.Servers)
				{
					luaTable3[num++] = ToLuaServer(server);
				}
			}
			luaTable["Servers"] = luaTable3;
		}
		catch
		{
		}
		return luaTable;
	}

	private LuaTable ToLuaServer(AccountServer s)
	{
		LuaTable luaTable = CreateLuaTable("server");
		try
		{
			luaTable["Id"] = s?.Id;
			luaTable["OptionId"] = s?.OptionId;
			luaTable["Name"] = s?.Name;
			luaTable["DefaultCharacterOptionId"] = s?.DefaultCharacterOptionId;
			luaTable["AccountId"] = s?.AccountId;
			LuaTable luaTable2 = CreateLuaTable("characters");
			int num = 1;
			if (s?.Characters != null)
			{
				foreach (AccountCharacter character in s.Characters)
				{
					luaTable2[num++] = ToLuaCharacter(character);
				}
			}
			luaTable["Characters"] = luaTable2;
		}
		catch
		{
		}
		return luaTable;
	}

	private LuaTable ToLuaCharacter(AccountCharacter c)
	{
		LuaTable luaTable = CreateLuaTable("character");
		try
		{
			luaTable["Id"] = c?.Id;
			luaTable["OptionId"] = c?.OptionId;
			luaTable["Name"] = c?.Name;
			luaTable["ServerId"] = c?.ServerId;
		}
		catch
		{
		}
		return luaTable;
	}

	private LuaTable ToLuaSquad(Squad s)
	{
		return ToLuaSquad(s, includeAccounts: true);
	}

	private LuaTable ToLuaSquad(Squad s, bool includeAccounts)
	{
		LuaTable luaTable = CreateLuaTable("squad");
		try
		{
			luaTable["Id"] = s?.Id;
			luaTable["Name"] = s?.Name;
			luaTable["OrderIndex"] = s?.OrderIndex;
			if (includeAccounts)
			{
				LuaTable luaTable2 = CreateLuaTable("accounts");
				int num = 1;
				if (s?.Accounts != null)
				{
					foreach (Account account in s.Accounts)
					{
						luaTable2[num++] = ToLuaAccount(account, includeSquad: true);
					}
				}
				luaTable["Accounts"] = luaTable2;
			}
		}
		catch
		{
		}
		return luaTable;
	}

	[LuaApiFunction(Name = "Account_ChangeAccountCb", Version = "v1", Category = "Account", Signature = "Account_ChangeAccountCb(accountId, function(ok) ... end)", Description = "Switch the active account", Snippet = "Account_ChangeAccountCb(accountId, function(ok)\n    __CURSOR__\nend)")]
	public void Account_ChangeAccountCb(string accountId, LuaFunction callback)
	{
		RunLifetimeTracker.BeginOp(_runId);
		Task.Run(async delegate
		{
			_ = 2;
			try
			{
				try
				{
				}
				catch
				{
				}
				if (_accountManager is AccountManager accountManager)
				{
					await accountManager.ChangeAccountAsync(opts: new AccountSwitchOptions(CreateFreshSession: true, BrowserSessionIsolationMode.SeparateProfile), accountId: accountId);
				}
				else
				{
					if (_browser != null)
					{
						try
						{
							await _browser.CreateNewSessionAsync(BrowserSessionIsolationMode.SeparateProfile);
						}
						catch
						{
						}
					}
					await _accountManager.ChangeAccountAsync(accountId);
				}
				try
				{
				}
				catch
				{
				}
				CallLuaVoid(callback, true);
			}
			catch (Exception)
			{
				try
				{
				}
				catch
				{
				}
				try
				{
					CallLuaVoid(callback, false);
				}
				catch
				{
				}
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "Browser_NavigateCb", Version = "v1", Category = "Browser", Signature = "Browser_NavigateCb(url, function() ... end)", Description = "Open a URL", Snippet = "Browser_NavigateCb(url, function()\n    __CURSOR__\nend)")]
	public void Browser_NavigateCb(string url, LuaFunction callback)
	{
		RunLifetimeTracker.BeginOp(_runId);
		_browser.NavigateAsync(url).ContinueWith(delegate(Task t)
		{
			try
			{
				CallLuaVoid(callback, t.IsCompletedSuccessfully);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "Browser_ReloadCb", Version = "v1", Category = "Browser", Signature = "Browser_ReloadCb(function() ... end)", Description = "Reload the page", Snippet = "Browser_ReloadCb(function()\n    __CURSOR__\nend)")]
	public void Browser_ReloadCb(LuaFunction callback)
	{
		RunLifetimeTracker.BeginOp(_runId);
		_browser.ReloadAsync().ContinueWith(delegate(Task t)
		{
			try
			{
				CallLuaVoid(callback, t.IsCompletedSuccessfully);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "Browser_ExecuteScriptCb", Version = "v1", Category = "Browser", Signature = "Browser_ExecuteScriptCb(jsCode, function(result) ... end)", Description = "Execute JavaScript and return the result", Snippet = "Browser_ExecuteScriptCb(jsCode, function(result)\n    __CURSOR__\nend)")]
	public void Browser_ExecuteScriptCb(string script, LuaFunction callback)
	{
		RunLifetimeTracker.BeginOp(_runId);
		_browser.ExecuteScriptAsync(script).ContinueWith(delegate(Task<string> t)
		{
			try
			{
				string text = (t.IsCompletedSuccessfully ? (t.Result ?? string.Empty) : string.Empty);
				CallLuaVoid(callback, text);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "Browser_ElementExistsCb", Version = "v1", Category = "Browser", Signature = "Browser_ElementExistsCb(selector, function(exists) ... end)", Description = "Check whether an element exists", Snippet = "Browser_ElementExistsCb(selector, function(exists)\n    __CURSOR__\nend)")]
	public void Browser_ElementExistsCb(string selector, LuaFunction callback)
	{
		RunLifetimeTracker.BeginOp(_runId);
		_browser.ElementExistsAsync(selector).ContinueWith(delegate(Task<bool> t)
		{
			try
			{
				CallLuaVoid(callback, t.IsCompletedSuccessfully && t.Result);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "Browser_WaitForElementCb", Version = "v1", Category = "Browser", Signature = "Browser_WaitForElementCb(selector, timeoutMs, function(found) ... end)", Description = "Wait for an element", Snippet = "Browser_WaitForElementCb(selector, timeoutMs, function(found)\n    __CURSOR__\nend)")]
	public void Browser_WaitForElementCb(string selector, int timeoutMs, LuaFunction callback)
	{
		RunLifetimeTracker.BeginOp(_runId);
		_browser.WaitForElementExistsAsync(selector, timeoutMs).ContinueWith(delegate(Task<bool> t)
		{
			try
			{
				CallLuaVoid(callback, t.IsCompletedSuccessfully && t.Result);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "Telegram_SendMessageCb", Version = "v1", Category = "Telegram", Signature = "Telegram_SendMessageCb(text, function(ok) ... end)", Description = "Send a Telegram message", Snippet = "Telegram_SendMessageCb('text', function(ok)\n    __CURSOR__\nend)")]
	public void Telegram_SendMessageCb(string text, LuaFunction callback)
	{
		try
		{
			string text2 = (text ?? string.Empty).Trim();
			if (text2.Length == 0)
			{
				CallLuaVoid(callback, false);
				return;
			}
			ModulesApiClient modulesApiClient = new ModulesApiClient();
			RunLifetimeTracker.BeginOp(_runId);
			modulesApiClient.SendTelegramMessageAsync(text2).ContinueWith(delegate(Task<bool> t)
			{
				try
				{
					bool flag = t.IsCompletedSuccessfully && t.Result;
					CallLuaVoid(callback, flag);
				}
				catch
				{
				}
				finally
				{
					try
					{
						RunLifetimeTracker.EndOp(_runId);
					}
					catch
					{
					}
				}
			});
		}
		catch
		{
			try
			{
				CallLuaVoid(callback, false);
			}
			catch
			{
			}
		}
	}

	[LuaApiFunction(Name = "Net_PostJsonCb", Version = "v1", Category = "Net", Signature = "Net_PostJsonCb(url, jsonBody, contentType, function(res) ... end)", Description = "Send an HTTP POST with JSON and return the response in res", Snippet = "Net_PostJsonCb(url, json, 'application/json', function(res)\n    __CURSOR__\nend)")]
	public void NetPostJsonSb(string url, string jsonBody, string contentType, LuaFunction callback)
	{
		HttpClient client = new HttpClient();
		HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
		StringContent content = new StringContent(jsonBody, Encoding.UTF8, contentType);
		request.Content = content;
		RunLifetimeTracker.BeginOp(_runId);
		client.SendAsync(request).ContinueWith(delegate(Task<HttpResponseMessage> t)
		{
			if (t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode)
			{
				t.Result.Content.ReadAsStringAsync().ContinueWith(delegate(Task<string> r)
				{
					try
					{
						object obj2 = CreateResponseContainer();
						SetField(obj2, "Success", t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode);
						SetField(obj2, "ResponseBody", r.IsCompletedSuccessfully ? r.Result : "");
						SetField(obj2, "Error", null);
						CallLuaVoid(callback, obj2);
					}
					finally
					{
						DisposeAll();
						try
						{
							RunLifetimeTracker.EndOp(_runId);
						}
						catch
						{
						}
					}
				});
			}
			else
			{
				object obj = CreateResponseContainer();
				SetField(obj, "Success", t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode);
				SetField(obj, "ResponseBody", null);
				SetField(obj, "Error", t.IsCompletedSuccessfully ? t.Result.ReasonPhrase : ((!t.IsFaulted) ? "Cancelled" : t.Exception?.GetBaseException()?.Message));
				CallLuaVoid(callback, obj);
				DisposeAll();
			}
		});
		object CreateResponseContainer()
		{
			if (_lua != null)
			{
				return CreateLuaTable("response");
			}
			return new Dictionary<string, object>();
		}
		void DisposeAll()
		{
			try
			{
				content.Dispose();
			}
			catch
			{
			}
			try
			{
				request.Dispose();
			}
			catch
			{
			}
			try
			{
				client.Dispose();
			}
			catch
			{
			}
		}
		static void SetField(object tableOrDict, string key, object? value)
		{
			try
			{
				if (tableOrDict is LuaTable luaTable)
				{
					luaTable[key] = value;
				}
				else if (tableOrDict is IDictionary<string, object> dictionary)
				{
					dictionary[key] = value;
				}
				else if (tableOrDict is IDictionary dictionary2)
				{
					dictionary2[key] = value;
				}
			}
			catch
			{
			}
		}
	}

	private BrowserManager? GetBrowserManager()
	{
		try
		{
			Application app = Application.Current;
			if (app == null)
			{
				return null;
			}
			MainWindow mainWindow = null;
			Dispatcher dispatcher = app.Dispatcher;
			mainWindow = ((dispatcher == null || !dispatcher.CheckAccess()) ? app.Dispatcher?.Invoke(delegate
			{
				object obj3 = app.MainWindow as MainWindow;
				if (obj3 == null)
				{
					WindowCollection windows = app.Windows;
					if (windows == null)
					{
						return (MainWindow)null;
					}
					IEnumerable<MainWindow> enumerable = windows.OfType<MainWindow>();
					if (enumerable == null)
					{
						return (MainWindow)null;
					}
					obj3 = enumerable.FirstOrDefault();
				}
				return (MainWindow)obj3;
			}) : ((app.MainWindow as MainWindow) ?? app.Windows?.OfType<MainWindow>()?.FirstOrDefault()));
			if (mainWindow == null)
			{
				return null;
			}
			BrowserManager browserManager = mainWindow.BrowserManager;
			if (browserManager != null)
			{
				return browserManager;
			}
			try
			{
				BrowserManager browserManager2 = new BrowserManager(mainWindow);
				(typeof(MainWindow).GetProperty("BrowserManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetSetMethod(nonPublic: true))?.Invoke(mainWindow, new object[1] { browserManager2 });
				return browserManager2;
			}
			catch
			{
				return null;
			}
		}
		catch
		{
			return null;
		}
	}

	[LuaApiFunction(Name = "BrowserV2_Create", Version = "v2", Category = "BrowserV2", Signature = "BrowserV2_Create(options, function(handle) ... end)", Description = "Create a browser and return its handle", Snippet = "BrowserV2_Create({}, function(handle)\n    __CURSOR__\nend)")]
	public void BrowserV2_Create(object options, LuaFunction callback)
	{
		BrowserManager browserManager = GetBrowserManager();
		if (browserManager == null)
		{
			CallLuaVoid(callback, 0);
			return;
		}
		BrowserManager.CreateOptions createOptions = new BrowserManager.CreateOptions();
		try
		{
			if (options is LuaTable luaTable)
			{
				string text = luaTable["StartUrl"] as string;
				if (!string.IsNullOrWhiteSpace(text))
				{
					createOptions.StartUrl = text;
				}
			}
		}
		catch
		{
		}
		RunLifetimeTracker.BeginOp(_runId);
		browserManager.CreateAsync(createOptions).ContinueWith(delegate(Task<int> t)
		{
			try
			{
				int num = (t.IsCompletedSuccessfully ? t.Result : 0);
				if (num > 0)
				{
					try
					{
						if (_runId.HasValue)
						{
							RunContextTracker.RegisterHandle(num, _runId.Value);
						}
						else
						{
							RunContextTracker.RegisterHandle(num);
						}
					}
					catch
					{
					}
				}
				CallLuaVoid(callback, num);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "BrowserV2_Close", Version = "v2", Category = "BrowserV2", Signature = "BrowserV2_Close(handle, function(ok) ... end)", Description = "Close a created browser", Snippet = "BrowserV2_Close(handle, function(ok)\n    __CURSOR__\nend)")]
	public void BrowserV2_Close(int handle, LuaFunction callback)
	{
		BrowserManager browserManager = GetBrowserManager();
		if (browserManager == null)
		{
			CallLuaVoid(callback, false);
			return;
		}
		RunLifetimeTracker.BeginOp(_runId);
		bool flag = browserManager.Close(handle);
		try
		{
			if (flag)
			{
				if (_runId.HasValue)
				{
					RunContextTracker.UnregisterHandle(handle, _runId.Value);
				}
				else
				{
					RunContextTracker.UnregisterHandle(handle);
				}
			}
		}
		catch
		{
		}
		finally
		{
			try
			{
				RunLifetimeTracker.EndOp(_runId);
			}
			catch
			{
			}
		}
		CallLuaVoid(callback, flag);
	}

	[LuaApiFunction(Name = "BrowserV2_Navigate", Version = "v2", Category = "BrowserV2", Signature = "BrowserV2_Navigate(handle, url, function(ok) ... end)", Description = "Open a URL in the specified browser", Snippet = "BrowserV2_Navigate(handle, url, function(ok)\n    __CURSOR__\nend)")]
	public void BrowserV2_Navigate(int handle, string url, LuaFunction callback)
	{
		BrowserManager browserManager = GetBrowserManager();
		if (browserManager == null || !browserManager.TryGet(handle, out var browser, out var _))
		{
			CallLuaVoid(callback, false);
			return;
		}
		RunLifetimeTracker.BeginOp(_runId);
		browser.NavigateAsync(url).ContinueWith(delegate(Task t)
		{
			try
			{
				CallLuaVoid(callback, t.IsCompletedSuccessfully);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "BrowserV2_Reload", Version = "v2", Category = "BrowserV2", Signature = "BrowserV2_Reload(handle, function(ok) ... end)", Description = "Reload the page", Snippet = "BrowserV2_Reload(handle, function(ok)\n    __CURSOR__\nend)")]
	public void BrowserV2_Reload(int handle, LuaFunction callback)
	{
		BrowserManager browserManager = GetBrowserManager();
		if (browserManager == null || !browserManager.TryGet(handle, out var browser, out var _))
		{
			CallLuaVoid(callback, false);
			return;
		}
		RunLifetimeTracker.BeginOp(_runId);
		browser.ReloadAsync().ContinueWith(delegate(Task t)
		{
			try
			{
				CallLuaVoid(callback, t.IsCompletedSuccessfully);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "BrowserV2_ExecuteScript", Version = "v2", Category = "BrowserV2", Signature = "BrowserV2_ExecuteScript(handle, jsCode, function(result) ... end)", Description = "Execute JavaScript in a v2 browser", Snippet = "BrowserV2_ExecuteScript(handle, jsCode, function(result)\n    __CURSOR__\nend)")]
	public void BrowserV2_ExecuteScript(int handle, string script, LuaFunction callback)
	{
		BrowserManager browserManager = GetBrowserManager();
		if (browserManager == null || !browserManager.TryGet(handle, out var browser, out var _))
		{
			CallLuaVoid(callback, string.Empty);
			return;
		}
		RunLifetimeTracker.BeginOp(_runId);
		browser.ExecuteScriptAsync(script).ContinueWith(delegate(Task<string> t)
		{
			try
			{
				string text = (t.IsCompletedSuccessfully ? (t.Result ?? string.Empty) : string.Empty);
				CallLuaVoid(callback, text);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "BrowserV2_ElementExists", Version = "v2", Category = "BrowserV2", Signature = "BrowserV2_ElementExists(handle, selector, function(exists) ... end)", Description = "Check whether an element exists", Snippet = "BrowserV2_ElementExists(handle, selector, function(exists)\n    __CURSOR__\nend)")]
	public void BrowserV2_ElementExists(int handle, string selector, LuaFunction callback)
	{
		BrowserManager browserManager = GetBrowserManager();
		if (browserManager == null || !browserManager.TryGet(handle, out var browser, out var _))
		{
			CallLuaVoid(callback, false);
			return;
		}
		RunLifetimeTracker.BeginOp(_runId);
		browser.ElementExistsAsync(selector).ContinueWith(delegate(Task<bool> t)
		{
			try
			{
				CallLuaVoid(callback, t.IsCompletedSuccessfully && t.Result);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "BrowserV2_WaitForElement", Version = "v2", Category = "BrowserV2", Signature = "BrowserV2_WaitForElement(handle, selector, timeoutMs, function(found) ... end)", Description = "Wait for an element", Snippet = "BrowserV2_WaitForElement(handle, selector, timeoutMs, function(found)\n    __CURSOR__\nend)")]
	public void BrowserV2_WaitForElement(int handle, string selector, int timeoutMs, LuaFunction callback)
	{
		BrowserManager browserManager = GetBrowserManager();
		if (browserManager == null || !browserManager.TryGet(handle, out var browser, out var _))
		{
			CallLuaVoid(callback, false);
			return;
		}
		RunLifetimeTracker.BeginOp(_runId);
		browser.WaitForElementExistsAsync(selector, timeoutMs).ContinueWith(delegate(Task<bool> t)
		{
			try
			{
				CallLuaVoid(callback, t.IsCompletedSuccessfully && t.Result);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "BrowserV2_ChangeAccount", Version = "v2", Category = "BrowserV2", Signature = "BrowserV2_ChangeAccount(handle, accountId, function(ok) ... end)", Description = "Switch the account in the specified v2 browser", Snippet = "BrowserV2_ChangeAccount(handle, accountId, function(ok)\n    __CURSOR__\nend)")]
	public void BrowserV2_ChangeAccount(int handle, string accountId, LuaFunction callback)
	{
		BrowserManager browserManager = GetBrowserManager();
		if (browserManager == null || !browserManager.TryGet(handle, out var _, out var accountManager))
		{
			CallLuaVoid(callback, false);
			return;
		}
		RunLifetimeTracker.BeginOp(_runId);
		accountManager.ChangeAccountAsync(accountId).ContinueWith(delegate(Task t)
		{
			try
			{
				CallLuaVoid(callback, t.IsCompletedSuccessfully && t.Exception == null);
			}
			catch
			{
			}
			finally
			{
				try
				{
					RunLifetimeTracker.EndOp(_runId);
				}
				catch
				{
				}
			}
		});
	}

	[LuaApiFunction(Name = "BrowserV2_GetCurrentAccount", Version = "v2", Category = "BrowserV2", Signature = "BrowserV2_GetCurrentAccount(handle, function(acc) ... end)", Description = "Return the current account as a Lua table", Snippet = "BrowserV2_GetCurrentAccount(handle, function(acc)\n    __CURSOR__\nend)")]
	public void BrowserV2_GetCurrentAccount(int handle, LuaFunction callback)
	{
		BrowserManager browserManager = GetBrowserManager();
		if (browserManager == null || !browserManager.TryGet(handle, out var _, out var accountManager))
		{
			CallLuaVoid(callback, null);
			return;
		}
		accountManager.GetAccountAsync().ContinueWith(delegate(Task<Account> t)
		{
			try
			{
				Account acc = (t.IsCompletedSuccessfully ? t.Result : null);
				LuaTable luaTable = ToLuaAccount(acc, includeSquad: true);
				CallLuaVoid(callback, luaTable);
			}
			catch
			{
			}
		});
	}
}

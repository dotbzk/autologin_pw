using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Pw.Hub.Abstractions;
using Pw.Hub.Controls;
using Pw.Hub.Infrastructure.Logging;

namespace Pw.Hub.Services;

public class BrowserManager
{
	private class Entry
	{
		public int Id { get; init; }

		public BrowserView View { get; init; }

		public IBrowser Browser { get; init; }

		public IAccountManager AccountManager { get; init; }
	}

	public class CreateOptions
	{
		public string StartUrl { get; set; }
	}

	private static readonly ILogger _log = Log.For<BrowserManager>();

	private readonly MainWindow _mainWindow;

	private int _nextId = 1;

	private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

	public BrowserManager(MainWindow mainWindow)
	{
		_mainWindow = mainWindow ?? throw new ArgumentNullException("mainWindow");
	}

	public async Task<int> CreateAsync(CreateOptions options = null)
	{
		Stopwatch sw = Stopwatch.StartNew();
		_log.Info("CreateAsync: start creating browser v2", new Dictionary<string, object> {
		{
			"startUrl",
			options?.StartUrl
		} });
		BrowserView view = null;
		await _mainWindow.Dispatcher.InvokeAsync(delegate
		{
			view = new BrowserView();
			_mainWindow.AddBrowserView(view);
		});
		WebCoreBrowser browser = new WebCoreBrowser(view);
		AccountManager am = new AccountManager(browser);
		try
		{
			await browser.CreateNewSessionAsync();
		}
		catch (Exception ex)
		{
			try
			{
				_log.Warn("CreateAsync: initial session failed", new Dictionary<string, object> { { "ex", ex.Message } });
			}
			catch
			{
			}
		}
		int id = _nextId++;
		_entries[id] = new Entry
		{
			Id = id,
			View = view,
			Browser = browser,
			AccountManager = am
		};
		if (!string.IsNullOrWhiteSpace(options?.StartUrl))
		{
			try
			{
				await browser.NavigateAsync(options.StartUrl);
			}
			catch (Exception ex2)
			{
				try
				{
					_log.Warn("CreateAsync: start navigation failed", new Dictionary<string, object> { { "ex", ex2.Message } });
				}
				catch
				{
				}
			}
		}
		sw.Stop();
		_log.Info("CreateAsync: created", new Dictionary<string, object>
		{
			{ "handle", id },
			{ "elapsedMs", sw.ElapsedMilliseconds }
		});
		return id;
	}

	public bool Close(int handle)
	{
		_log.Info("Close: requested", new Dictionary<string, object> { { "handle", handle } });
		if (!_entries.TryGetValue(handle, out var value))
		{
			_log.Warn("Close: not found", new Dictionary<string, object> { { "handle", handle } });
			return false;
		}
		string profileDir = null;
		try
		{
			if (value.Browser is WebCoreBrowser webCoreBrowser)
			{
				profileDir = webCoreBrowser.UserDataFolder;
			}
		}
		catch
		{
		}
		try
		{
			_mainWindow.RemoveBrowserView(value.View);
		}
		catch
		{
		}
		try
		{
			value.View?.Current?.Dispose();
		}
		catch
		{
		}
		_entries.Remove(handle);
		try
		{
			if (!string.IsNullOrWhiteSpace(profileDir))
			{
				Task.Run(async delegate
				{
					try
					{
						await WebCoreBrowser.TryDeleteDirForExternal(profileDir);
						_log.Debug("Close: profile cleanup scheduled", new Dictionary<string, object> { { "dir", profileDir } });
					}
					catch
					{
					}
				});
			}
		}
		catch
		{
		}
		_log.Info("Close: completed", new Dictionary<string, object> { { "handle", handle } });
		return true;
	}

	public int[] List()
	{
		return _entries.Keys.OrderBy((int x) => x).ToArray();
	}

	public bool TryGet(int handle, out IBrowser browser, out IAccountManager accountManager)
	{
		if (_entries.TryGetValue(handle, out var value))
		{
			browser = value.Browser;
			accountManager = value.AccountManager;
			return true;
		}
		browser = null;
		accountManager = null;
		return false;
	}
}

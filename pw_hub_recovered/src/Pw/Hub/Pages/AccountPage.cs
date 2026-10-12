using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Pw.Hub.Abstractions;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Services;
using Pw.Hub.Tools;

namespace Pw.Hub.Pages;

public class AccountPage : UserControl, IWebViewHost, INotifyPropertyChanged, IComponentConnector
{
	public IAccountManager AccountManager;

	public IBrowser Browser;

	public LuaScriptRunner LuaRunner;

	private bool _isNoAccountOverlayVisible = true;

	private bool _isLoading;

	private static Dictionary<string, string> _jsCode = new Dictionary<string, string>();

	internal Button BtnBack;

	internal Button BtnRefresh;

	internal Button BtnForward;

	internal Button BtnHome;

	internal Button BtnPromo;

	internal Button BtnMdm;

	internal TextBox AddressBox;

	internal WebView2 Wv;

	private bool _contentLoaded;

	public bool IsCoreInitialized => Wv?.CoreWebView2?.CookieManager != null;

	public bool IsNoAccountOverlayVisible
	{
		get
		{
			return _isNoAccountOverlayVisible;
		}
		set
		{
			if (value != _isNoAccountOverlayVisible)
			{
				_isNoAccountOverlayVisible = value;
				OnPropertyChanged("IsNoAccountOverlayVisible");
			}
		}
	}

	public bool IsLoading
	{
		get
		{
			return _isLoading;
		}
		set
		{
			if (value != _isLoading)
			{
				_isLoading = value;
				OnPropertyChanged("IsLoading");
			}
		}
	}

	public WebView2 Current => Wv;

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged(string name)
	{
		try
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
		catch
		{
		}
	}

	public Task ReplaceAsync(WebView2 newControl)
	{
		return ReplaceWebViewControlAsync(newControl);
	}

	public Task PreloadAsync(WebView2 newControl)
	{
		return PreloadWebViewControlAsync(newControl);
	}

	public Task FinalizeSwapAsync(WebView2 newControl)
	{
		return FinalizeSwapWebViewControlAsync(newControl);
	}

	public AccountPage()
	{
		InitializeComponent();
		base.DataContext = this;
		Wv.Source = new Uri("https://pwonline.ru");
		Wv.NavigationCompleted += WvOnNavigationCompleted;
		Wv.NavigationStarting += WvOnNavigationStarting;
		Browser = new WebCoreBrowser(this, BrowserSessionIsolationMode.InPrivate);
		AccountManager = new AccountManager(Browser);
		if (AccountManager is AccountManager accountManager)
		{
			accountManager.EnsureNewSessionBeforeSwitchAsync = async delegate
			{
				_ = 1;
				try
				{
					await Browser.CreateNewSessionAsync(BrowserSessionIsolationMode.InPrivate);
					FingerprintProfile profile = FingerprintGenerator.Generate();
					await Browser.ApplyAntiDetectAsync(profile);
				}
				catch
				{
				}
			};
		}
		try
		{
			UpdateNavUi();
		}
		catch
		{
		}
		try
		{
			AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty;
		}
		catch
		{
		}
		try
		{
			AccountManager.CurrentAccountChanged += OnCurrentAccountChanged;
		}
		catch
		{
		}
		LuaRunner = new LuaScriptRunner(AccountManager, Browser);
	}

	private static string GetJs(string fileName)
	{
		try
		{
			if (_jsCode.TryGetValue(fileName, out var value))
			{
				return value;
			}
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "promo", fileName);
			if (File.Exists(path))
			{
				_jsCode[fileName] = File.ReadAllText(path);
				return _jsCode[fileName];
			}
		}
		catch
		{
		}
		return "";
	}

	private async Task<string> ExecuteJsModule(string fileName)
	{
		try
		{
			string js = GetJs(fileName);
			return await Browser.ExecuteScriptAsync(js);
		}
		catch
		{
			return null;
		}
	}

	private void OnCurrentAccountChanged(Account account)
	{
		try
		{
			if (!base.Dispatcher.CheckAccess())
			{
				base.Dispatcher.InvokeAsync(delegate
				{
					OnCurrentAccountChanged(account);
				});
			}
			else if (account != null)
			{
				IsNoAccountOverlayVisible = false;
				try
				{
					AccountManager.CurrentAccountChanged -= OnCurrentAccountChanged;
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

	private async Task ReplaceWebViewControlAsync(WebView2 newWv)
	{
		if (newWv == null)
		{
			return;
		}
		try
		{
			Wv.NavigationCompleted -= WvOnNavigationCompleted;
		}
		catch
		{
		}
		try
		{
			Wv.NavigationStarting -= WvOnNavigationStarting;
		}
		catch
		{
		}
		try
		{
			Wv.NavigationCompleted -= Wv_OnNavigationCompleted;
		}
		catch
		{
		}
		try
		{
			Wv.CoreWebView2InitializationCompleted -= Wv_OnCoreWebView2InitializationCompleted;
		}
		catch
		{
		}
		try
		{
			Wv.CoreWebView2.HistoryChanged -= CoreWebView2OnHistoryChanged;
		}
		catch
		{
		}
		Grid grid = Wv.Parent as Grid;
		int row = Grid.GetRow(Wv);
		int column = Grid.GetColumn(Wv);
		int rowSpan = Grid.GetRowSpan(Wv);
		int columnSpan = Grid.GetColumnSpan(Wv);
		int num = grid?.Children.IndexOf(Wv) ?? (-1);
		try
		{
			Wv.Dispose();
		}
		catch
		{
		}
		grid?.Children.Remove(Wv);
		if (grid != null)
		{
			if (num >= 0)
			{
				grid.Children.Insert(num, newWv);
			}
			else
			{
				grid.Children.Add(newWv);
			}
			Grid.SetRow(newWv, row);
			Grid.SetColumn(newWv, column);
			Grid.SetRowSpan(newWv, rowSpan);
			Grid.SetColumnSpan(newWv, columnSpan);
		}
		Wv = newWv;
		try
		{
			Wv.NavigationCompleted += WvOnNavigationCompleted;
		}
		catch
		{
		}
		try
		{
			Wv.NavigationStarting += WvOnNavigationStarting;
		}
		catch
		{
		}
		try
		{
			Wv.NavigationCompleted += Wv_OnNavigationCompleted;
		}
		catch
		{
		}
		try
		{
			Wv.CoreWebView2InitializationCompleted += Wv_OnCoreWebView2InitializationCompleted;
		}
		catch
		{
		}
		try
		{
			await Wv.EnsureCoreWebView2Async();
		}
		catch
		{
		}
	}

	private async Task PreloadWebViewControlAsync(WebView2 newWv)
	{
		if (newWv == null || !(Wv.Parent is Grid grid))
		{
			return;
		}
		int row = Grid.GetRow(Wv);
		int column = Grid.GetColumn(Wv);
		int rowSpan = Grid.GetRowSpan(Wv);
		int columnSpan = Grid.GetColumnSpan(Wv);
		newWv.Visibility = Visibility.Hidden;
		if (!grid.Children.Contains(newWv))
		{
			int num = grid.Children.IndexOf(Wv);
			if (num >= 0)
			{
				grid.Children.Insert(num, newWv);
			}
			else
			{
				grid.Children.Add(newWv);
			}
		}
		Grid.SetRow(newWv, row);
		Grid.SetColumn(newWv, column);
		Grid.SetRowSpan(newWv, rowSpan);
		Grid.SetColumnSpan(newWv, columnSpan);
		try
		{
			await newWv.EnsureCoreWebView2Async();
		}
		catch
		{
		}
	}

	private async Task FinalizeSwapWebViewControlAsync(WebView2 newWv)
	{
		if (newWv == null)
		{
			return;
		}
		try
		{
			Wv.NavigationCompleted -= WvOnNavigationCompleted;
		}
		catch
		{
		}
		try
		{
			Wv.NavigationStarting -= WvOnNavigationStarting;
		}
		catch
		{
		}
		try
		{
			Wv.NavigationCompleted -= Wv_OnNavigationCompleted;
		}
		catch
		{
		}
		try
		{
			Wv.CoreWebView2InitializationCompleted -= Wv_OnCoreWebView2InitializationCompleted;
		}
		catch
		{
		}
		Grid grid = Wv.Parent as Grid;
		try
		{
			Wv.Dispose();
		}
		catch
		{
		}
		try
		{
			grid?.Children.Remove(Wv);
		}
		catch
		{
		}
		Wv = newWv;
		try
		{
			Wv.NavigationCompleted += WvOnNavigationCompleted;
		}
		catch
		{
		}
		try
		{
			Wv.NavigationStarting += WvOnNavigationStarting;
		}
		catch
		{
		}
		try
		{
			Wv.NavigationCompleted += Wv_OnNavigationCompleted;
		}
		catch
		{
		}
		try
		{
			Wv.CoreWebView2InitializationCompleted += Wv_OnCoreWebView2InitializationCompleted;
		}
		catch
		{
		}
		try
		{
			Wv.Visibility = Visibility.Visible;
		}
		catch
		{
		}
		try
		{
			await Wv.EnsureCoreWebView2Async();
		}
		catch
		{
		}
		try
		{
			if (Wv?.CoreWebView2 != null)
			{
				try
				{
					Wv.CoreWebView2.Settings.IsWebMessageEnabled = true;
				}
				catch
				{
				}
				try
				{
					Wv.CoreWebView2.WebMessageReceived -= CoreWebView2OnWebMessageReceived;
				}
				catch
				{
				}
				try
				{
					Wv.CoreWebView2.WebMessageReceived += CoreWebView2OnWebMessageReceived;
				}
				catch
				{
				}
				try
				{
					Wv.CoreWebView2.HistoryChanged -= CoreWebView2OnHistoryChanged;
				}
				catch
				{
				}
				try
				{
					Wv.CoreWebView2.HistoryChanged += CoreWebView2OnHistoryChanged;
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

	private void WvOnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
	{
		bool flag = false;
		try
		{
			flag = e.Uri.StartsWith("https://pwonline.ru") || e.Uri.StartsWith("http://pwonline.ru") || e.Uri.StartsWith("https://pw.mail.ru") || e.Uri.StartsWith("http://pw.mail.ru");
		}
		catch
		{
		}
		if (!flag)
		{
			try
			{
				IsLoading = false;
			}
			catch
			{
			}
			e.Cancel = true;
			return;
		}
		try
		{
			IsLoading = true;
		}
		catch
		{
		}
	}

	private async void WvOnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
	{
		_ = 11;
		try
		{
			IsLoading = false;
			AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty;
			UpdateNavUi();
			if (Browser.Source.AbsoluteUri.Contains("promo_items.php"))
			{
				string text = JsonSerializer.Serialize(AccountManager?.CurrentAccount?.PromoAccInfo ?? string.Empty, JsonSerializerOptions.Web);
				await Browser.ExecuteScriptAsync("window.__pwLastAccInfo = " + text + ";");
				await ExecuteJsModule("promo_core.js");
				if (!Browser.Source.AbsoluteUri.Contains("do=activate"))
				{
					await ExecuteJsModule("promo_init.js");
					await ExecuteJsModule("promo_manage_popup.js");
					await ExecuteJsModule("promo_character_select.js");
					await ExecuteJsModule("promo_submit_ui.js");
					await ExecuteJsModule("promo_async_submit.js");
				}
				await ExecuteJsModule("promo_submit_hook.js");
				await ExecuteJsModule("promo_ping.js");
				await ExecuteJsModule("promo_chests_popup.js");
				await ExecuteJsModule("promo_transfer_history_popup.js");
				try
				{
					await Browser.ExecuteScriptAsync("try{ if (window.Promo && Promo.autoRun) Promo.autoRun(); }catch(e){}");
					return;
				}
				catch
				{
					return;
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private void Wv_OnCoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
	{
		try
		{
			Wv.DefaultBackgroundColor = Color.FromArgb(255, 30, 30, 30);
		}
		catch
		{
		}
		try
		{
			Wv.CoreWebView2.Settings.IsWebMessageEnabled = true;
		}
		catch
		{
		}
		try
		{
			CoreWebView2 coreWebView = Wv.CoreWebView2;
			string javaScript = "(function(){\r\n  try{\r\n    if (!window.pwHubAppConfig){\r\n      var pending = new Map();\r\n      var nextId = 1;\r\n      function tryLocalGet(key, def){ try{ var raw = localStorage.getItem(key); return raw!=null ? JSON.parse(raw) : def; }catch(_){ return def; } }\r\n      function tryLocalSet(key, val){ try{ localStorage.setItem(key, JSON.stringify(val)); }catch(_){ }\r\n      }\r\n\r\n      var shim = {\r\n        get: function(key, def){\r\n          return new Promise(function(resolve){\r\n            var id = nextId++;\r\n            pending.set(id, resolve);\r\n            try{\r\n              if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){\r\n                window.chrome.webview.postMessage({ type: 'appConfig:get', id: id, key: key });\r\n              } else {\r\n                resolve(tryLocalGet(key, def));\r\n                return;\r\n              }\r\n            } catch(_){\r\n              resolve(tryLocalGet(key, def));\r\n              return;\r\n            }\r\n            setTimeout(function(){\r\n              if (pending.has(id)){\r\n                pending.delete(id);\r\n                resolve(tryLocalGet(key, def));\r\n              }\r\n            }, 800);\r\n          });\r\n        },\r\n        set: function(key, val){\r\n          return new Promise(function(resolve){\r\n            var id = nextId++;\r\n            pending.set(id, resolve);\r\n            try{\r\n              if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){\r\n                window.chrome.webview.postMessage({ type: 'appConfig:set', id: id, key: key, value: val });\r\n              } else {\r\n                tryLocalSet(key, val);\r\n                resolve(true);\r\n                return;\r\n              }\r\n            } catch(_){\r\n              tryLocalSet(key, val);\r\n              resolve(true);\r\n              return;\r\n            }\r\n            setTimeout(function(){\r\n              if (pending.has(id)){\r\n                pending.delete(id);\r\n                tryLocalSet(key, val);\r\n                resolve(true);\r\n              }\r\n            }, 800);\r\n          });\r\n        }\r\n      };\r\n\r\n      // Official Pw.Hub configuration channel for injected scripts.\r\n      window.pwHubAppConfig = shim;\r\n\r\n      // Extend window.appConfig without replacing existing fields or methods.\r\n      try{\r\n        if (!window.appConfig){ window.appConfig = {}; }\r\n        if (!window.appConfig.get){ window.appConfig.get = shim.get; }\r\n        if (!window.appConfig.set){ window.appConfig.set = shim.set; }\r\n      }catch(_){ }\r\n\r\n      if (window.chrome && window.chrome.webview){\r\n        window.chrome.webview.addEventListener('message', function(ev){\r\n          try{\r\n            var msg = ev && ev.data;\r\n            if (!msg || msg.type !== 'appConfig:resp') return;\r\n            var res = pending.get(msg.id);\r\n            if (!res) return;\r\n            pending.delete(msg.id);\r\n            if ('value' in msg) res(msg.value); else res(true);\r\n          }catch(__){}\r\n        });\r\n      }\r\n    }\r\n  }catch(__){}\r\n})();";
			try
			{
				coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(javaScript);
			}
			catch
			{
			}
		}
		catch
		{
		}
		try
		{
			Wv.CoreWebView2.HistoryChanged += CoreWebView2OnHistoryChanged;
		}
		catch
		{
		}
		try
		{
			Wv.CoreWebView2.WebMessageReceived -= CoreWebView2OnWebMessageReceived;
		}
		catch
		{
		}
		try
		{
			Wv.CoreWebView2.WebMessageReceived += CoreWebView2OnWebMessageReceived;
		}
		catch
		{
		}
		try
		{
			UpdateNavUi();
		}
		catch
		{
		}
		try
		{
			AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty;
		}
		catch
		{
		}
		Browser.SetCookieAsync(Array.Empty<Cookie>());
	}

	private async void Wv_OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
	{
		try
		{
			IsLoading = false;
		}
		catch
		{
		}
		if (AccountManager.CurrentAccount == null || !(await Browser.WaitForElementExistsAsync(".auth_h > h2 > a > strong", 500)))
		{
			return;
		}
		string text = await Browser.ExecuteScriptAsync("document.querySelector(\"div.user_photo > span > a > img\").src");
		if (string.IsNullOrEmpty(text) || text.Contains("null"))
		{
			return;
		}
		string text2 = text.Trim('"');
		bool flag = false;
		if (text2 != AccountManager.CurrentAccount.ImageSource)
		{
			AccountManager.CurrentAccount.ImageSource = text2;
			flag = true;
		}
		if (string.IsNullOrEmpty(AccountManager.CurrentAccount.SiteId))
		{
			Account currentAccount = AccountManager.CurrentAccount;
			currentAccount.SiteId = await AccountManager.GetSiteId();
			flag = true;
		}
		if (!flag)
		{
			return;
		}
		await using AppDbContext db = new AppDbContext();
		db.Update(AccountManager.CurrentAccount);
		try
		{
			await db.SaveChangesAsync();
		}
		catch
		{
		}
	}

	private void CoreWebView2OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		try
		{
			string text = null;
			try
			{
				text = e.TryGetWebMessageAsString();
			}
			catch
			{
			}
			if (string.IsNullOrWhiteSpace(text))
			{
				try
				{
					text = e.WebMessageAsJson;
				}
				catch
				{
				}
			}
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(text);
			JsonElement rootElement = jsonDocument.RootElement;
			if (rootElement.ValueKind == JsonValueKind.String)
			{
				try
				{
					using JsonDocument jsonDocument2 = JsonDocument.Parse(rootElement.GetString() ?? "{}");
					HandleWebPayload(jsonDocument2.RootElement);
					return;
				}
				catch
				{
				}
			}
			HandleWebPayload(rootElement);
		}
		catch
		{
		}
	}

	private void HandleWebPayload(JsonElement root)
	{
		string doVal;
		string accInfo;
		List<string> cartItems;
		try
		{
			if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var value))
			{
				return;
			}
			string a = value.GetString();
			if (string.Equals(a, "appConfig:get", StringComparison.Ordinal))
			{
				try
				{
					IAppConfigService service = App.Services.GetService<IAppConfigService>();
					if (service == null)
					{
						return;
					}
					JsonElement value3;
					int value2 = (root.TryGetProperty("id", out value3) ? value3.GetInt32() : 0);
					JsonElement value4;
					string text = (root.TryGetProperty("key", out value4) ? value4.GetString() : null);
					string value5 = null;
					if (!string.IsNullOrEmpty(text))
					{
						service.TryGetString(text, out value5);
					}
					string value6 = value5 ?? "null";
					string webMessageAsJson = $"{{\"type\":\"appConfig:resp\",\"id\":{value2},\"value\":{value6}}}";
					try
					{
						Wv.CoreWebView2?.PostWebMessageAsJson(webMessageAsJson);
						return;
					}
					catch
					{
						return;
					}
				}
				catch
				{
					return;
				}
			}
			if (string.Equals(a, "appConfig:set", StringComparison.Ordinal))
			{
				try
				{
					IAppConfigService service2 = App.Services.GetService<IAppConfigService>();
					if (service2 == null)
					{
						return;
					}
					JsonElement value7;
					int id = (root.TryGetProperty("id", out value7) ? value7.GetInt32() : 0);
					JsonElement value8;
					string text2 = (root.TryGetProperty("key", out value8) ? value8.GetString() : null);
					if (!string.IsNullOrEmpty(text2))
					{
						if (root.TryGetProperty("value", out var value9))
						{
							string rawText = value9.GetRawText();
							service2.SetString(text2, rawText);
						}
						else
						{
							service2.SetString(text2, "null");
						}
					}
					string webMessageAsJson2 = JsonSerializer.Serialize(new
					{
						type = "appConfig:resp",
						id = id,
						ok = true
					});
					try
					{
						Wv.CoreWebView2?.PostWebMessageAsJson(webMessageAsJson2);
					}
					catch
					{
					}
					try
					{
						service2.SaveAsync();
						return;
					}
					catch
					{
						return;
					}
				}
				catch
				{
					return;
				}
			}
			if (string.Equals(a, "promo_log", StringComparison.Ordinal))
			{
				try
				{
					if (root.TryGetProperty("event", out var value10))
					{
						value10.GetString();
					}
					if (root.TryGetProperty("ts", out var value11))
					{
						value11.GetInt64();
					}
					if (root.TryGetProperty("data", out var value12))
					{
						try
						{
							string rawText2 = value12.GetRawText();
							if (!string.IsNullOrWhiteSpace(rawText2) && rawText2.Length > 500)
							{
								_ = rawText2.Substring(0, 500) + "...";
							}
						}
						catch
						{
						}
					}
					try
					{
						return;
					}
					catch
					{
						return;
					}
				}
				catch
				{
					return;
				}
			}
			if (string.Equals(a, "promo_ping", StringComparison.Ordinal) || string.Equals(a, "promo_ping2", StringComparison.Ordinal) || !string.Equals(a, "promo_form_submit", StringComparison.Ordinal))
			{
				return;
			}
			doVal = (root.TryGetProperty("do", out var value13) ? (value13.GetString() ?? string.Empty) : string.Empty);
			accInfo = (root.TryGetProperty("acc_info", out var value14) ? (value14.GetString() ?? string.Empty) : string.Empty);
			cartItems = new List<string>();
			if (root.TryGetProperty("cart_items", out var value15) && value15.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement item in value15.EnumerateArray())
				{
					try
					{
						cartItems.Add(item.ToString());
					}
					catch
					{
					}
				}
			}
			if (!base.Dispatcher.CheckAccess())
			{
				base.Dispatcher.Invoke(apply);
			}
			else
			{
				apply();
			}
		}
		catch
		{
		}
		void apply()
		{
			Account account = AccountManager?.CurrentAccount;
			if (account == null)
			{
				return;
			}
			try
			{
				account.PromoDo = doVal;
			}
			catch
			{
			}
			try
			{
				account.PromoAccInfo = accInfo;
			}
			catch
			{
			}
			try
			{
				account.PromoCartItems = cartItems;
			}
			catch
			{
			}
			try
			{
				account.PromoLastSubmittedAt = DateTime.Now;
			}
			catch
			{
			}
			using AppDbContext appDbContext = new AppDbContext();
			appDbContext.Update(account);
			appDbContext.SaveChanges();
		}
	}

	private void CoreWebView2OnHistoryChanged(object? sender, object e)
	{
		try
		{
			UpdateNavUi();
		}
		catch
		{
		}
	}

	private void UpdateNavUi()
	{
		try
		{
			CoreWebView2 coreWebView = Wv?.CoreWebView2;
			if (coreWebView == null)
			{
				BtnBack.IsEnabled = false;
				BtnForward.IsEnabled = false;
				BtnRefresh.IsEnabled = false;
			}
			else
			{
				BtnBack.IsEnabled = coreWebView.CanGoBack;
				BtnForward.IsEnabled = coreWebView.CanGoForward;
				BtnRefresh.IsEnabled = true;
			}
		}
		catch
		{
		}
		try
		{
			AddressBox.Text = Wv?.Source?.ToString() ?? AddressBox.Text;
		}
		catch
		{
		}
	}

	private void BtnBack_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			WebView2 wv = Wv;
			if (wv != null && wv.CoreWebView2?.CanGoBack == true)
			{
				Wv.CoreWebView2.GoBack();
			}
		}
		catch
		{
		}
		try
		{
			UpdateNavUi();
		}
		catch
		{
		}
	}

	private void BtnForward_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			WebView2 wv = Wv;
			if (wv != null && wv.CoreWebView2?.CanGoForward == true)
			{
				Wv.CoreWebView2.GoForward();
			}
		}
		catch
		{
		}
		try
		{
			UpdateNavUi();
		}
		catch
		{
		}
	}

	private void BtnRefresh_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			if (Wv?.CoreWebView2 != null)
			{
				Wv.CoreWebView2.Reload();
			}
		}
		catch
		{
		}
		try
		{
			UpdateNavUi();
		}
		catch
		{
		}
	}

	private void BtnHome_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			if (Wv?.CoreWebView2 != null)
			{
				Wv.CoreWebView2.Navigate("https://pwonline.ru/");
			}
			else
			{
				Wv.Source = new Uri("https://pwonline.ru/");
			}
		}
		catch
		{
		}
	}

	private void BtnPromo_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			if (Wv?.CoreWebView2 != null)
			{
				Wv.CoreWebView2.Navigate("https://pwonline.ru/promo_items.php");
			}
			else
			{
				Wv.Source = new Uri("https://pwonline.ru/promo_items.php");
			}
		}
		catch
		{
		}
	}

	private void BtnMdm_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			if (Wv?.CoreWebView2 != null)
			{
				Wv.CoreWebView2.Navigate("https://pwonline.ru/chests2.php");
			}
			else
			{
				Wv.Source = new Uri("https://pwonline.ru/chests2.php");
			}
		}
		catch
		{
		}
	}

	private void AddressBox_OnKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key != Key.Return)
		{
			return;
		}
		string text = AddressBox.Text?.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		string text2 = BuildAllowedUrl(text);
		if (text2 == null)
		{
			return;
		}
		try
		{
			if (Wv?.CoreWebView2 != null)
			{
				Wv.CoreWebView2.Navigate(text2);
			}
			else
			{
				Wv.Source = new Uri(text2);
			}
		}
		catch
		{
		}
	}

	private static string BuildAllowedUrl(string input)
	{
		try
		{
			string text = input;
			if (text.StartsWith("/"))
			{
				text = "https://pwonline.ru" + text;
			}
			else if (!text.StartsWith("http://") && !text.StartsWith("https://"))
			{
				text = "https://" + text;
			}
			if (text.Equals("https://pwonline.ru", StringComparison.OrdinalIgnoreCase))
			{
				text += "/";
			}
			return (text.StartsWith("https://pwonline.ru") || text.StartsWith("http://pwonline.ru") || text.StartsWith("https://pw.mail.ru") || text.StartsWith("http://pw.mail.ru")) ? text : null;
		}
		catch
		{
			return null;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/Pw.Hub;component/pages/accountpage.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			BtnBack = (Button)target;
			BtnBack.Click += BtnBack_OnClick;
			break;
		case 2:
			BtnRefresh = (Button)target;
			BtnRefresh.Click += BtnRefresh_OnClick;
			break;
		case 3:
			BtnForward = (Button)target;
			BtnForward.Click += BtnForward_OnClick;
			break;
		case 4:
			BtnHome = (Button)target;
			BtnHome.Click += BtnHome_OnClick;
			break;
		case 5:
			BtnPromo = (Button)target;
			BtnPromo.Click += BtnPromo_OnClick;
			break;
		case 6:
			BtnMdm = (Button)target;
			BtnMdm.Click += BtnMdm_OnClick;
			break;
		case 7:
			AddressBox = (TextBox)target;
			AddressBox.KeyDown += AddressBox_OnKeyDown;
			break;
		case 8:
			Wv = (WebView2)target;
			Wv.NavigationCompleted += Wv_OnNavigationCompleted;
			Wv.CoreWebView2InitializationCompleted += Wv_OnCoreWebView2InitializationCompleted;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

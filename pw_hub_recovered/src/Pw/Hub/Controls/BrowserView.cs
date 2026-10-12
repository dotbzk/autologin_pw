using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
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
using Pw.Hub.Services;

namespace Pw.Hub.Controls;

public class BrowserView : UserControl, IWebViewHost, INotifyPropertyChanged, IComponentConnector
{
	private bool _isLoading;

	internal Button BtnBack;

	internal Button BtnRefresh;

	internal Button BtnForward;

	internal TextBox AddressBox;

	internal WebView2 Wv;

	private bool _contentLoaded;

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

	public BrowserView()
	{
		InitializeComponent();
		try
		{
			Wv.Visibility = Visibility.Visible;
		}
		catch
		{
		}
		UpdateNavUi();
		try
		{
			AddressBox.Text = Wv?.Source?.ToString() ?? string.Empty;
		}
		catch
		{
		}
	}

	public async Task ReplaceAsync(WebView2 newControl)
	{
		if (newControl == null)
		{
			return;
		}
		try
		{
			Wv.NavigationStarting -= OnNavigationStartingFilter;
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
		if (!(base.Content is Grid grid))
		{
			base.Content = newControl;
			Wv = newControl;
			AttachHandlers();
			try
			{
				await Wv.EnsureCoreWebView2Async();
				return;
			}
			catch
			{
				return;
			}
		}
		grid.Children.Clear();
		grid.Children.Add(newControl);
		try
		{
			Grid.SetRow(newControl, 1);
		}
		catch
		{
		}
		try
		{
			Grid.SetColumn(newControl, 0);
		}
		catch
		{
		}
		Wv = newControl;
		AttachHandlers();
		try
		{
			await Wv.EnsureCoreWebView2Async();
		}
		catch
		{
		}
	}

	public async Task PreloadAsync(WebView2 newControl)
	{
		if (newControl == null || !(base.Content is Grid grid))
		{
			return;
		}
		try
		{
			newControl.Visibility = Visibility.Hidden;
		}
		catch
		{
		}
		try
		{
			newControl.DefaultBackgroundColor = Color.FromArgb(255, 30, 30, 30);
		}
		catch
		{
		}
		grid.Children.Add(newControl);
		Grid.SetRow(newControl, 1);
		Grid.SetColumn(newControl, 0);
		try
		{
			newControl.NavigationStarting += OnNavigationStartingFilter;
		}
		catch
		{
		}
		try
		{
			newControl.NavigationCompleted += Wv_OnNavigationCompleted;
		}
		catch
		{
		}
		try
		{
			newControl.CoreWebView2InitializationCompleted += Wv_OnCoreWebView2InitializationCompleted;
		}
		catch
		{
		}
		try
		{
			await newControl.EnsureCoreWebView2Async();
		}
		catch
		{
		}
		try
		{
			newControl.DefaultBackgroundColor = Color.FromArgb(255, 30, 30, 30);
		}
		catch
		{
		}
	}

	public async Task FinalizeSwapAsync(WebView2 newControl)
	{
		if (newControl == null || !(base.Content is Grid grid))
		{
			return;
		}
		if (grid.Children.Count > 0)
		{
			WebView2 wv = Wv;
			if (wv != newControl)
			{
				try
				{
					wv.NavigationStarting -= OnNavigationStartingFilter;
				}
				catch
				{
				}
				try
				{
					wv.NavigationCompleted -= Wv_OnNavigationCompleted;
				}
				catch
				{
				}
				try
				{
					wv.CoreWebView2InitializationCompleted -= Wv_OnCoreWebView2InitializationCompleted;
				}
				catch
				{
				}
				try
				{
					wv.CoreWebView2.HistoryChanged -= CoreWebView2OnHistoryChanged;
				}
				catch
				{
				}
				try
				{
					wv.Dispose();
				}
				catch
				{
				}
				try
				{
					grid.Children.Remove(wv);
				}
				catch
				{
				}
			}
		}
		Wv = newControl;
		AttachHandlers();
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
	}

	private void AttachHandlers()
	{
		try
		{
			Wv.NavigationStarting += OnNavigationStartingFilter;
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
			Wv.CoreWebView2.HistoryChanged += CoreWebView2OnHistoryChanged;
		}
		catch
		{
		}
	}

	private void CoreWebView2OnHistoryChanged(object? sender, object e)
	{
		UpdateNavUi();
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

	private void OnNavigationStartingFilter(object sender, CoreWebView2NavigationStartingEventArgs e)
	{
		try
		{
			if (!e.Uri.StartsWith("https://pwonline.ru") && !e.Uri.StartsWith("http://pwonline.ru") && !e.Uri.StartsWith("https://pw.mail.ru") && !e.Uri.StartsWith("http://pw.mail.ru"))
			{
				e.Cancel = true;
				IsLoading = false;
			}
			else
			{
				IsLoading = true;
			}
		}
		catch
		{
		}
	}

	private void Wv_OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
	{
		try
		{
			IsLoading = false;
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
		UpdateNavUi();
	}

	private void Wv_OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
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
			Wv.CoreWebView2.HistoryChanged += CoreWebView2OnHistoryChanged;
		}
		catch
		{
		}
		try
		{
			CoreWebView2 coreWebView = Wv.CoreWebView2;
			string javaScript = "(function(){\r\n  try{\r\n    if (!window.appConfig){\r\n      var pending = new Map();\r\n      var nextId = 1;\r\n      function tryLocalGet(key, def){ try{ var raw = localStorage.getItem(key); return raw!=null ? JSON.parse(raw) : def; }catch(_){ return def; } }\r\n      function tryLocalSet(key, val){ try{ localStorage.setItem(key, JSON.stringify(val)); }catch(_){ } }\r\n      window.appConfig = {\r\n        get: function(key, def){ return new Promise(function(resolve){ var id = nextId++; pending.set(id, resolve); try{ window.chrome && window.chrome.webview && window.chrome.webview.postMessage({ type: 'appConfig:get', id: id, key: key }); } catch(_){ resolve(tryLocalGet(key, def)); return; } setTimeout(function(){ if (pending.has(id)){ pending.delete(id); resolve(tryLocalGet(key, def)); } }, 800); }); },\r\n        set: function(key, val){ return new Promise(function(resolve){ var id = nextId++; pending.set(id, resolve); try{ window.chrome && window.chrome.webview && window.chrome.webview.postMessage({ type: 'appConfig:set', id: id, key: key, value: val }); } catch(_){ tryLocalSet(key, val); resolve(true); return; } setTimeout(function(){ if (pending.has(id)){ pending.delete(id); tryLocalSet(key, val); resolve(true); } }, 800); }); }\r\n      };\r\n      if (window.chrome && window.chrome.webview){\r\n        window.chrome.webview.addEventListener('message', function(ev){ try{ var msg = ev && ev.data; if (!msg || msg.type !== 'appConfig:resp') return; var res = pending.get(msg.id); if (!res) return; pending.delete(msg.id); if ('value' in msg) res(msg.value); else res(true); } catch(__){} });\r\n      }\r\n    }\r\n  }catch(__){}\r\n})();";
			try
			{
				coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(javaScript);
			}
			catch
			{
			}
			try
			{
				coreWebView.WebMessageReceived += CoreOnWebMessageReceived;
			}
			catch
			{
			}
		}
		catch
		{
		}
		UpdateNavUi();
	}

	private void CoreOnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		try
		{
			IAppConfigService service = App.Services.GetService<IAppConfigService>();
			if (service == null)
			{
				return;
			}
			string webMessageAsJson = e.WebMessageAsJson;
			if (string.IsNullOrWhiteSpace(webMessageAsJson))
			{
				return;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(webMessageAsJson);
			JsonElement rootElement = jsonDocument.RootElement;
			if (!rootElement.TryGetProperty("type", out var value))
			{
				return;
			}
			string text = value.GetString();
			if (text == "appConfig:get")
			{
				JsonElement value3;
				int value2 = (rootElement.TryGetProperty("id", out value3) ? value3.GetInt32() : 0);
				JsonElement value4;
				string text2 = (rootElement.TryGetProperty("key", out value4) ? value4.GetString() : null);
				string value5 = null;
				if (!string.IsNullOrEmpty(text2))
				{
					service.TryGetString(text2, out value5);
				}
				string value6 = value5 ?? "null";
				string webMessageAsJson2 = $"{{\"type\":\"appConfig:resp\",\"id\":{value2},\"value\":{value6}}}";
				Wv.CoreWebView2.PostWebMessageAsJson(webMessageAsJson2);
			}
			else
			{
				if (!(text == "appConfig:set"))
				{
					return;
				}
				JsonElement value7;
				int id = (rootElement.TryGetProperty("id", out value7) ? value7.GetInt32() : 0);
				JsonElement value8;
				string text3 = (rootElement.TryGetProperty("key", out value8) ? value8.GetString() : null);
				if (!string.IsNullOrEmpty(text3))
				{
					if (rootElement.TryGetProperty("value", out var value9))
					{
						string rawText = value9.GetRawText();
						service.SetString(text3, rawText);
					}
					else
					{
						service.SetString(text3, "null");
					}
				}
				string webMessageAsJson3 = JsonSerializer.Serialize(new
				{
					type = "appConfig:resp",
					id = id,
					ok = true
				});
				Wv.CoreWebView2.PostWebMessageAsJson(webMessageAsJson3);
				service.SaveAsync();
			}
		}
		catch
		{
		}
	}

	private async void BtnBack_OnClick(object sender, RoutedEventArgs e)
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
		UpdateNavUi();
	}

	private async void BtnForward_OnClick(object sender, RoutedEventArgs e)
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
		UpdateNavUi();
	}

	private async void BtnRefresh_OnClick(object sender, RoutedEventArgs e)
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
		UpdateNavUi();
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
			Uri resourceLocator = new Uri("/Pw.Hub;component/controls/browserview.xaml", UriKind.Relative);
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
			AddressBox = (TextBox)target;
			AddressBox.KeyDown += AddressBox_OnKeyDown;
			break;
		case 5:
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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Pw.Hub.Abstractions;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public class WebCoreBrowser : IBrowser
{
	private WebView2 _webView;

	private readonly IWebViewHost _host;

	private readonly BrowserSessionIsolationMode _mode;

	private string _userDataDir;

	private bool _profileIsPersistent;

	public bool CurrentProfileWasCreated { get; private set; }

	public string UserDataFolder => _userDataDir;

	public Uri Source
	{
		get
		{
			try
			{
				WebView2 webView = _webView;
				if (webView != null && webView.Dispatcher?.CheckAccess() == true)
				{
					return _webView.Source;
				}
				return _webView.Dispatcher.Invoke(() => _webView.Source);
			}
			catch
			{
				try
				{
					return new Uri("https://pwonline.ru");
				}
				catch
				{
				}
				return new Uri("about:blank", UriKind.RelativeOrAbsolute);
			}
		}
	}

	public WebCoreBrowser(IWebViewHost host, BrowserSessionIsolationMode mode = BrowserSessionIsolationMode.SeparateProfile)
	{
		_host = host;
		_mode = mode;
		_webView = host.Current;
	}

	private async Task<T> OnUiAsync<T>(Func<Task<T>> func)
	{
		if (_webView.Dispatcher.CheckAccess())
		{
			return await func();
		}
		return await _webView.Dispatcher.InvokeAsync(func, DispatcherPriority.Normal).Task.Unwrap();
	}

	private async Task OnUiAsync(Func<Task> func)
	{
		if (_webView.Dispatcher.CheckAccess())
		{
			await func();
		}
		else
		{
			await _webView.Dispatcher.InvokeAsync(func, DispatcherPriority.Normal).Task;
		}
	}

	private async Task EnsureCoreAndSetBackgroundAsync()
	{
		await _webView.EnsureCoreWebView2Async();
		try
		{
			_webView.DefaultBackgroundColor = Color.FromArgb(255, 30, 30, 30);
		}
		catch
		{
		}
	}

	public Task<string> ExecuteScriptAsync(string script)
	{
		return OnUiAsync(async delegate
		{
			await EnsureCoreAndSetBackgroundAsync();
			return (await _webView.ExecuteScriptAsync(script)).Trim('"');
		});
	}

	public Task NavigateAsync(string url)
	{
		return OnUiAsync(async delegate
		{
			if (PwNavigationPolicy.IsAllowedUrl(url))
			{
				await EnsureCoreAndSetBackgroundAsync();
				_webView.CoreWebView2.Navigate(url);
			}
		});
	}

	public Task ReloadAsync()
	{
		return OnUiAsync(async delegate
		{
			await EnsureCoreAndSetBackgroundAsync();
			_webView.CoreWebView2.Reload();
		});
	}

	public async Task<bool> ElementExistsAsync(string selector)
	{
		string script = "\r\n            (function() {\r\n                return document.querySelector('" + selector + "') !== null;\r\n            })()";
		return (await ExecuteScriptAsync(script)).Trim().ToLower() == "true";
	}

	public async Task<bool> WaitForElementExistsAsync(string selector, int timeoutMs = 5000)
	{
		int elapsed = 0;
		for (int interval = 50; elapsed < timeoutMs; elapsed += interval)
		{
			if (await ElementExistsAsync(selector))
			{
				return true;
			}
			await Task.Delay(interval);
		}
		return false;
	}

	public Task<Cookie[]> GetCookiesAsync()
	{
		return OnUiAsync(async delegate
		{
			await EnsureCoreAndSetBackgroundAsync();
			return (await _webView.CoreWebView2.CookieManager.GetCookiesAsync(null)).Select(Cookie.FromCoreWebView2Cookie).ToArray();
		});
	}

	public Task SetCookieAsync(Cookie[] cookie)
	{
		return OnUiAsync(async delegate
		{
			await EnsureCoreAndSetBackgroundAsync();
			CoreWebView2CookieManager cookieManager = _webView.CoreWebView2.CookieManager;
			cookieManager.DeleteAllCookies();
			AddOrUpdateCookies(cookieManager, cookie);
		});
	}

	public Task ImportCookiesAsync(Cookie[] cookies)
	{
		return OnUiAsync(async delegate
		{
			await EnsureCoreAndSetBackgroundAsync();
			AddOrUpdateCookies(_webView.CoreWebView2.CookieManager, cookies);
		});
	}

	private static void AddOrUpdateCookies(CoreWebView2CookieManager cookieManager, Cookie[] cookies)
	{
		foreach (Cookie cookie in cookies ?? Array.Empty<Cookie>())
		{
			CoreWebView2Cookie coreCookie = cookieManager.CreateCookie(cookie.Name, cookie.Value, cookie.Domain, cookie.Path);
			if (!cookie.IsSession && cookie.Expires > DateTime.UnixEpoch)
			{
				coreCookie.Expires = cookie.Expires;
			}
			coreCookie.IsHttpOnly = cookie.IsHttpOnly;
			coreCookie.IsSecure = cookie.IsSecure;
			coreCookie.SameSite = cookie.SameSite;
			cookieManager.AddOrUpdateCookie(coreCookie);
		}
	}

	public Task CreateNewSessionAsync(BrowserSessionIsolationMode? overrideMode = null, string? persistentProfileId = null)
	{
		return OnUiAsync(async delegate
		{
			Uri previousUri = _webView?.Source ?? new Uri("https://pwonline.ru");
			string oldDir = _userDataDir;
			bool oldProfileIsPersistent = _profileIsPersistent;
			string newDir = null;
			CoreWebView2CreationProperties coreWebView2CreationProperties = new CoreWebView2CreationProperties();
			BrowserSessionIsolationMode effMode = overrideMode ?? _mode;
			if (effMode == BrowserSessionIsolationMode.SeparateProfile)
			{
				if (string.IsNullOrWhiteSpace(persistentProfileId))
				{
					newDir = CreateProfileDir();
					CurrentProfileWasCreated = true;
				}
				else
				{
					newDir = GetPersistentProfileDir(persistentProfileId, out bool created);
					CurrentProfileWasCreated = created;
				}
				coreWebView2CreationProperties.UserDataFolder = newDir;
			}
			else
			{
				CurrentProfileWasCreated = true;
				coreWebView2CreationProperties.IsInPrivateModeEnabled = true;
			}
			WebView2 newWv = new WebView2
			{
				CreationProperties = coreWebView2CreationProperties
			};
			try
			{
				newWv.Visibility = Visibility.Hidden;
			}
			catch
			{
			}
			try
			{
				newWv.DefaultBackgroundColor = Color.FromArgb(255, 30, 30, 30);
			}
			catch
			{
			}
			await _host.PreloadAsync(newWv);
			try
			{
				await newWv.EnsureCoreWebView2Async();
			}
			catch
			{
			}
			try
			{
				newWv.DefaultBackgroundColor = Color.FromArgb(255, 30, 30, 30);
			}
			catch
			{
			}
			newWv.Source = previousUri;
			_webView = newWv;
			_userDataDir = ((effMode == BrowserSessionIsolationMode.SeparateProfile) ? newDir : null);
			_profileIsPersistent = effMode == BrowserSessionIsolationMode.SeparateProfile
				&& !string.IsNullOrWhiteSpace(persistentProfileId);
			bool finalized = false;
			try
			{
				newWv.CoreWebView2.NavigationStarting += delegate
				{
					EnsureFinalize();
				};
			}
			catch
			{
				EnsureFinalize();
			}
			Task.Run(async delegate
			{
				await Task.Delay(500);
				EnsureFinalize();
			});
			if (!oldProfileIsPersistent && !string.IsNullOrWhiteSpace(oldDir))
			{
				TryDeleteDirWithRetries(oldDir);
			}
			void EnsureFinalize()
			{
				if (!finalized)
				{
					finalized = true;
					OnUiAsync(async delegate
					{
						try
						{
							await _host.FinalizeSwapAsync(newWv);
						}
						catch
						{
						}
					});
				}
			}
		});
	}

	public async Task ApplyAntiDetectAsync(FingerprintProfile profile)
	{
		if (profile == null)
		{
			return;
		}
		await OnUiAsync(async delegate
		{
			await EnsureCoreAndSetBackgroundAsync();
			try
			{
				_webView.CoreWebView2.Settings.UserAgent = profile.UserAgent;
			}
			catch
			{
			}
			string value = string.Join(",", profile.Languages.Select((string l) => "\"" + l + "\""));
			int timezoneOffsetMinutes = profile.TimezoneOffsetMinutes;
			int hardwareConcurrency = profile.HardwareConcurrency;
			double deviceMemory = profile.DeviceMemory;
			int screenWidth = profile.ScreenWidth;
			int screenHeight = profile.ScreenHeight;
			int colorDepth = profile.ColorDepth;
			string value2 = profile.Platform ?? "Win32";
			string value3 = profile.Vendor ?? "Google Inc.";
			string value4 = profile.VendorSub ?? string.Empty;
			string value5 = profile.Product ?? "Gecko";
			string value6 = profile.ProductSub ?? "20030107";
			string value7 = profile.WebglVendor ?? "ANGLE (Intel)";
			string value8 = profile.WebglRenderer ?? "ANGLE (Intel, Intel(R) UHD Graphics)";
			double canvasNoise = profile.CanvasNoise;
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(2999, 19);
			defaultInterpolatedStringHandler.AppendLiteral("\r\n(function() {\r\n  try {\r\n    const define = (obj, prop, val) => {\r\n      try { Object.defineProperty(obj, prop, { get: () => val, configurable: true }); } catch {}\r\n    };\r\n\r\n    // navigator spoof\r\n    const nav = navigator;\r\n    define(nav, 'platform', '");
			defaultInterpolatedStringHandler.AppendFormatted(value2);
			defaultInterpolatedStringHandler.AppendLiteral("');\r\n    define(nav, 'vendor', '");
			defaultInterpolatedStringHandler.AppendFormatted(value3);
			defaultInterpolatedStringHandler.AppendLiteral("');\r\n    define(nav, 'vendorSub', '");
			defaultInterpolatedStringHandler.AppendFormatted(value4);
			defaultInterpolatedStringHandler.AppendLiteral("');\r\n    define(nav, 'product', '");
			defaultInterpolatedStringHandler.AppendFormatted(value5);
			defaultInterpolatedStringHandler.AppendLiteral("');\r\n    define(nav, 'productSub', '");
			defaultInterpolatedStringHandler.AppendFormatted(value6);
			defaultInterpolatedStringHandler.AppendLiteral("');\r\n    define(nav, 'hardwareConcurrency', ");
			defaultInterpolatedStringHandler.AppendFormatted(hardwareConcurrency);
			defaultInterpolatedStringHandler.AppendLiteral(");\r\n    define(nav, 'deviceMemory', ");
			defaultInterpolatedStringHandler.AppendFormatted(deviceMemory);
			defaultInterpolatedStringHandler.AppendLiteral(");\r\n    define(nav, 'language', ");
			List<string> languages = profile.Languages;
			defaultInterpolatedStringHandler.AppendFormatted((languages != null && languages.Count > 0) ? ("'" + profile.Languages[0] + "'") : "'en-US'");
			defaultInterpolatedStringHandler.AppendLiteral(");\r\n    Object.defineProperty(nav, 'languages', { get: () => [");
			defaultInterpolatedStringHandler.AppendFormatted(value);
			defaultInterpolatedStringHandler.AppendLiteral("], configurable: true });\r\n    define(nav, 'userAgent', '");
			defaultInterpolatedStringHandler.AppendFormatted(JavaScriptEncoder.Default.Encode(profile.UserAgent ?? "Mozilla/5.0"));
			defaultInterpolatedStringHandler.AppendLiteral("');\r\n\r\n    // screen spoof\r\n    const scr = screen;\r\n    define(scr, 'width', ");
			defaultInterpolatedStringHandler.AppendFormatted(screenWidth);
			defaultInterpolatedStringHandler.AppendLiteral(");\r\n    define(scr, 'height', ");
			defaultInterpolatedStringHandler.AppendFormatted(screenHeight);
			defaultInterpolatedStringHandler.AppendLiteral(");\r\n    define(scr, 'availWidth', ");
			defaultInterpolatedStringHandler.AppendFormatted(screenWidth);
			defaultInterpolatedStringHandler.AppendLiteral(");\r\n    define(scr, 'availHeight', ");
			defaultInterpolatedStringHandler.AppendFormatted(screenHeight - 40);
			defaultInterpolatedStringHandler.AppendLiteral(");\r\n    define(scr, 'colorDepth', ");
			defaultInterpolatedStringHandler.AppendFormatted(colorDepth);
			defaultInterpolatedStringHandler.AppendLiteral(");\r\n\r\n    // timezone spoof\r\n    const _Date = Date;\r\n    const RealDate = _Date;\r\n    function ZonedDate(...args) { return new RealDate(...args); }\r\n    ZonedDate.prototype = RealDate.prototype;\r\n    ZonedDate.UTC = RealDate.UTC;\r\n    ZonedDate.parse = RealDate.parse;\r\n    ZonedDate.now = RealDate.now;\r\n    RealDate.prototype.getTimezoneOffset = function() { return ");
			defaultInterpolatedStringHandler.AppendFormatted(timezoneOffsetMinutes);
			defaultInterpolatedStringHandler.AppendLiteral("; };\r\n    window.Date = ZonedDate;\r\n\r\n    // Intl spoof\r\n    const _rdto = Intl.DateTimeFormat.prototype.resolvedOptions;\r\n    Intl.DateTimeFormat.prototype.resolvedOptions = function(...args) {\r\n      const o = _rdto.apply(this, args);\r\n      try { o.timeZone = undefined; } catch {}\r\n      return o;\r\n    };\r\n\r\n    // Canvas noise\r\n    const addNoise = (canvas, ctx) => {\r\n      try {\r\n        const w = canvas.width, h = canvas.height;\r\n        const imgData = ctx.getImageData(0,0,w,h);\r\n        const d = imgData.data;\r\n        const n = ");
			defaultInterpolatedStringHandler.AppendFormatted(canvasNoise.ToString(CultureInfo.InvariantCulture));
			defaultInterpolatedStringHandler.AppendLiteral(";\r\n        for (let i=0;i<d.length;i+=4) {\r\n          d[i]   = Math.min(255, Math.max(0, d[i]   + (Math.random()-0.5)*n*10));\r\n          d[i+1] = Math.min(255, Math.max(0, d[i+1] + (Math.random()-0.5)*n*10));\r\n          d[i+2] = Math.min(255, Math.max(0, d[i+2] + (Math.random()-0.5)*n*10));\r\n        }\r\n        ctx.putImageData(imgData,0,0);\r\n      } catch {}\r\n    };\r\n    ['toDataURL','toBlob'].forEach(fn => {\r\n      try {\r\n        const orig = HTMLCanvasElement.prototype[fn];\r\n        HTMLCanvasElement.prototype[fn] = function(...args) {\r\n          const ctx = this.getContext('2d');\r\n          if (ctx) addNoise(this, ctx);\r\n          return orig.apply(this, args);\r\n        };\r\n      } catch {}\r\n    });\r\n\r\n    // WebGL spoof\r\n    const spoofGL = (proto) => {\r\n      if (!proto) return;\r\n      try {\r\n        const origParam = proto.getParameter;\r\n        proto.getParameter = function(param) {\r\n          if (param === 37445) return '");
			defaultInterpolatedStringHandler.AppendFormatted(value7);
			defaultInterpolatedStringHandler.AppendLiteral("'; // UNMASKED_VENDOR_WEBGL\r\n          if (param === 37446) return '");
			defaultInterpolatedStringHandler.AppendFormatted(value8);
			defaultInterpolatedStringHandler.AppendLiteral("'; // UNMASKED_RENDERER_WEBGL\r\n          return origParam.apply(this, arguments);\r\n        };\r\n      } catch {}\r\n    };\r\n    spoofGL(WebGLRenderingContext?.prototype);\r\n    spoofGL(WebGL2RenderingContext?.prototype);\r\n\r\n  } catch {}\r\n})();\r\n");
			string javaScript = defaultInterpolatedStringHandler.ToStringAndClear();
			try
			{
				await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(javaScript);
			}
			catch
			{
			}
		});
	}

	private static string ProfilesRoot()
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameToolsHub", "PwHub", "Profiles");
			Directory.CreateDirectory(text);
			return text;
		}
		catch
		{
			string text2 = Path.Combine(Path.GetTempPath(), "PwHub", "WebViewProfiles");
			try
			{
				Directory.CreateDirectory(text2);
			}
			catch
			{
			}
			return text2;
		}
	}

	private static string GetPersistentProfileDir(string accountId, out bool created)
	{
		byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(accountId));
		string profileName = Convert.ToHexString(hash).ToLowerInvariant()[..32];
		string path = Path.Combine(ProfilesRoot(), profileName);
		created = !Directory.Exists(path);
		Directory.CreateDirectory(path);
		return path;
	}

	private static string CreateProfileDir()
	{
		string path = ProfilesRoot();
		int num = 0;
		string text;
		do
		{
			string path2 = Guid.NewGuid().ToString("N");
			text = Path.Combine(path, path2);
			num++;
		}
		while (Directory.Exists(text) && num < 5);
		try
		{
			Directory.CreateDirectory(text);
		}
		catch
		{
		}
		return text;
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

	public static Task<bool> TryDeleteDirForExternal(string path)
	{
		return TryDeleteDirWithRetries(path);
	}
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Pw.Hub.Abstractions;
using Pw.Hub.Infrastructure.Logging;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public class WebCoreBrowser : IBrowser
{
	private static readonly ILogger _log = Log.For<WebCoreBrowser>();

	private WebView2 _webView;

	private readonly IWebViewHost _host;

	private readonly BrowserSessionIsolationMode _mode;

	private string _userDataDir;

	private bool _profileIsPersistent;

	private readonly SemaphoreSlim _recoveryGate = new SemaphoreSlim(1, 1);

	private BrowserSessionIsolationMode _activeMode;

	private CoreWebView2 _subscribedCore;

	private bool _browserProcessFailed;

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
		_activeMode = mode;
		_webView = host.Current;
	}

	private async Task<T> OnUiAsync<T>(Func<Task<T>> func)
	{
		if (_webView.Dispatcher.CheckAccess())
		{
			return await ExecuteWithBrowserRecoveryAsync(func);
		}
		return await _webView.Dispatcher.InvokeAsync(
			() => ExecuteWithBrowserRecoveryAsync(func), DispatcherPriority.Normal).Task.Unwrap();
	}

	private async Task OnUiAsync(Func<Task> func)
	{
		if (_webView.Dispatcher.CheckAccess())
		{
			await ExecuteWithBrowserRecoveryAsync(func);
		}
		else
		{
			await _webView.Dispatcher.InvokeAsync(
				() => ExecuteWithBrowserRecoveryAsync(func), DispatcherPriority.Normal).Task.Unwrap();
		}
	}

	private async Task<T> ExecuteWithBrowserRecoveryAsync<T>(Func<Task<T>> func)
	{
		try
		{
			return await func();
		}
		catch (InvalidOperationException ex) when (IsBrowserProcessFailure(ex))
		{
			LogInvalidControlDetected(ex);
			_browserProcessFailed = true;
			await RecoverBrowserAsync();
			return await func();
		}
	}

	private async Task ExecuteWithBrowserRecoveryAsync(Func<Task> func)
	{
		try
		{
			await func();
		}
		catch (InvalidOperationException ex) when (IsBrowserProcessFailure(ex))
		{
			LogInvalidControlDetected(ex);
			_browserProcessFailed = true;
			await RecoverBrowserAsync();
			await func();
		}
	}

	private async Task EnsureCoreAndSetBackgroundAsync()
	{
		if (_browserProcessFailed)
		{
			await RecoverBrowserAsync();
		}
		try
		{
			await _webView.EnsureCoreWebView2Async();
			SubscribeToProcessFailed();
		}
		catch (InvalidOperationException ex) when (IsBrowserProcessFailure(ex))
		{
			LogInvalidControlDetected(ex);
			_browserProcessFailed = true;
			await RecoverBrowserAsync();
		}
		try
		{
			_webView.DefaultBackgroundColor = Color.FromArgb(255, 30, 30, 30);
		}
		catch
		{
		}
	}

	private static bool IsBrowserProcessFailure(InvalidOperationException ex)
	{
		string message = ex?.Message ?? string.Empty;
		return message.Contains("browser process crashed", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("WebView control is no longer valid", StringComparison.OrdinalIgnoreCase);
	}

	private void SubscribeToProcessFailed()
	{
		CoreWebView2 core = _webView?.CoreWebView2;
		if (core == null || ReferenceEquals(core, _subscribedCore))
		{
			return;
		}
		try
		{
			if (_subscribedCore != null)
			{
				_subscribedCore.ProcessFailed -= CoreWebView2OnProcessFailed;
			}
		}
		catch
		{
		}
		_subscribedCore = core;
		core.ProcessFailed += CoreWebView2OnProcessFailed;
	}

	private void CoreWebView2OnProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
	{
		if (ReferenceEquals(sender, _subscribedCore))
		{
			_browserProcessFailed = true;
			LogProcessFailure(sender as CoreWebView2, e);
		}
	}

	private void LogProcessFailure(CoreWebView2 core, CoreWebView2ProcessFailedEventArgs e)
	{
		try
		{
			Dictionary<string, object> context = new Dictionary<string, object>
			{
				{ "режимСессии", _activeMode.ToString() },
				{ "постоянныйПрофиль", _profileIsPersistent },
				{ "папкаПрофиляНазначена", !string.IsNullOrWhiteSpace(_userDataDir) },
				{ "версияОС", Environment.OSVersion.VersionString },
				{ "архитектураОС", RuntimeInformation.OSArchitecture.ToString() },
				{ "архитектураПроцесса", RuntimeInformation.ProcessArchitecture.ToString() },
				{ "версияDotNet", RuntimeInformation.FrameworkDescription },
				{ "версияPwHub", typeof(WebCoreBrowser).Assembly.GetName().Version?.ToString() ?? "неизвестно" },
				{ "рабочийНаборПроцессаБайт", Environment.WorkingSet },
				{ "управляемаяПамятьБайт", GC.GetTotalMemory(forceFullCollection: false) }
			};
			TryAddContext(context, "видСбоя", () => e.ProcessFailedKind.ToString());
			TryAddContext(context, "причина", () => e.Reason.ToString());
			TryAddContext(context, "кодЗавершения", () => e.ExitCode);
			TryAddContext(context, "описаниеПроцесса", () => e.ProcessDescription);
			TryAddContext(context, "модульПроверкиЦелостности", () =>
			{
				string path = e.FailureSourceModulePath;
				return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFileName(path);
			});
			TryAddContext(context, "версияWebView2", () => core?.Environment?.BrowserVersionString);
			TryAddContext(context, "папкаОтчетовОСбоях", () => core?.Environment?.FailureReportFolderPath);
			_log.Error("Процесс WebView2 завершился со сбоем", null, context);
		}
		catch
		{
		}
	}

	private static void TryAddContext(Dictionary<string, object> context, string key, Func<object> valueFactory)
	{
		try
		{
			object value = valueFactory();
			if (value != null && (!(value is string text) || !string.IsNullOrWhiteSpace(text)))
			{
				context[key] = value;
			}
		}
		catch
		{
		}
	}

	private void LogInvalidControlDetected(InvalidOperationException ex)
	{
		try
		{
			_log.Warn("Обнаружен недействительный контрол WebView2; запускается восстановление", new Dictionary<string, object>
			{
				{ "типИсключения", ex.GetType().FullName ?? ex.GetType().Name },
				{ "сообщение", ex.Message },
				{ "режимСессии", _activeMode.ToString() },
				{ "постоянныйПрофиль", _profileIsPersistent }
			});
		}
		catch
		{
		}
	}

	private async Task RecoverBrowserAsync()
	{
		await _recoveryGate.WaitAsync();
		try
		{
			if (!_browserProcessFailed)
			{
				return;
			}
			Uri previousUri = Source;
			try
			{
				_log.Info("Начато восстановление WebView2", new Dictionary<string, object>
				{
					{ "режимСессии", _activeMode.ToString() },
					{ "постоянныйПрофиль", _profileIsPersistent },
					{ "папкаПрофиляНазначена", !string.IsNullOrWhiteSpace(_userDataDir) }
				});
			}
			catch
			{
			}
			Exception lastError = null;
			for (int attempt = 1; attempt <= 3; attempt++)
			{
				WebView2 replacement = new WebView2
				{
					CreationProperties = CreateRecoveryProperties()
				};
				try
				{
					replacement.DefaultBackgroundColor = Color.FromArgb(255, 30, 30, 30);
					await _host.ReplaceAsync(replacement);
					_webView = replacement;
					await replacement.EnsureCoreWebView2Async();
					SubscribeToProcessFailed();
					_browserProcessFailed = false;
					try
					{
						Dictionary<string, object> recoveryContext = new Dictionary<string, object>
						{
							{ "попытка", attempt },
							{ "режимСессии", _activeMode.ToString() },
							{ "постоянныйПрофиль", _profileIsPersistent }
						};
						TryAddContext(recoveryContext, "версияWebView2", () => replacement.CoreWebView2.Environment.BrowserVersionString);
						TryAddContext(recoveryContext, "папкаОтчетовОСбоях", () => replacement.CoreWebView2.Environment.FailureReportFolderPath);
						_log.Info("WebView2 успешно восстановлен", recoveryContext);
					}
					catch
					{
					}
					if (previousUri != null && PwNavigationPolicy.IsAllowedUrl(previousUri.ToString()))
					{
						replacement.CoreWebView2.Navigate(previousUri.ToString());
					}
					return;
				}
				catch (Exception ex)
				{
					lastError = ex;
					try
					{
						_log.Warn("Попытка восстановления WebView2 завершилась ошибкой", new Dictionary<string, object>
						{
							{ "попытка", attempt },
							{ "всегоПопыток", 3 },
							{ "типИсключения", ex.GetType().FullName ?? ex.GetType().Name },
							{ "сообщение", ex.Message }
						});
					}
					catch
					{
					}
					try
					{
						replacement.Dispose();
					}
					catch
					{
					}
					if (attempt < 3)
					{
						await Task.Delay(attempt * 250);
					}
				}
			}
			try
			{
				_log.Error("Не удалось восстановить WebView2 после трех попыток", lastError, new Dictionary<string, object>
				{
					{ "режимСессии", _activeMode.ToString() },
					{ "постоянныйПрофиль", _profileIsPersistent }
				});
			}
			catch
			{
			}
			throw new InvalidOperationException("Не удалось восстановить встроенный браузер после сбоя процесса WebView2.", lastError);
		}
		finally
		{
			_recoveryGate.Release();
		}
	}

	private CoreWebView2CreationProperties CreateRecoveryProperties()
	{
		CoreWebView2CreationProperties properties = new CoreWebView2CreationProperties();
		if (_activeMode == BrowserSessionIsolationMode.SeparateProfile && !string.IsNullOrWhiteSpace(_userDataDir))
		{
			properties.UserDataFolder = _userDataDir;
		}
		else if (_activeMode == BrowserSessionIsolationMode.InPrivate)
		{
			properties.IsInPrivateModeEnabled = true;
		}
		return properties;
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
			_activeMode = effMode;
			_userDataDir = ((effMode == BrowserSessionIsolationMode.SeparateProfile) ? newDir : null);
			_profileIsPersistent = effMode == BrowserSessionIsolationMode.SeparateProfile
				&& !string.IsNullOrWhiteSpace(persistentProfileId);
			try
			{
				SubscribeToProcessFailed();
			}
			catch
			{
			}
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

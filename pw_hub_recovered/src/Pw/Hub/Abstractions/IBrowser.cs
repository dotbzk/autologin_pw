using System;
using System.Threading.Tasks;
using Pw.Hub.Models;

namespace Pw.Hub.Abstractions;

public interface IBrowser
{
	Uri Source { get; }

	Task<string> ExecuteScriptAsync(string script);

	Task NavigateAsync(string url);

	Task ReloadAsync();

	Task<bool> ElementExistsAsync(string selector);

	Task<bool> WaitForElementExistsAsync(string selector, int timeoutMs = 5000);

	Task<Cookie[]> GetCookiesAsync();

	Task SetCookieAsync(Cookie[] cookie);

	Task CreateNewSessionAsync(BrowserSessionIsolationMode? overrideMode = null);

	Task ApplyAntiDetectAsync(FingerprintProfile profile);
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Abstractions;
using Pw.Hub.Infrastructure;
using Pw.Hub.Infrastructure.Logging;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public class AccountManager(IBrowser browser) : IAccountManager
{
	private static readonly ILogger _log = Log.For<AccountManager>();

	private const string CookieFileExtension = ".json";

	private const string CookieFolder = "Cookies";

	private volatile bool _isChanging;

	private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

	private PropertyChangedEventHandler _accountPropertyChangedHandler;

	private const string FingerprintFolder = "Fingerprints";

	private const string FingerprintFileExtension = ".fp.json";

	public Account CurrentAccount { get; private set; }

	public bool IsChanging => _isChanging;

	public Func<Task>? EnsureNewSessionBeforeSwitchAsync { get; set; }

	public event Action<Account> CurrentAccountChanged;

	public event Action<Account> CurrentAccountDataChanged;

	public event Action<bool> CurrentAccountChanging;

	public async Task ChangeAccountAsync(string accountId, AccountSwitchOptions opts)
	{
		await _semaphoreSlim.WaitAsync();
		SetChanging(value: true);
		try
		{
			Stopwatch.StartNew();
			_log.Info("ChangeAccount(v1): start", new Dictionary<string, object>
			{
				{ "accountId", accountId },
				{ "createFreshSession", opts.CreateFreshSession },
				{
					"sessionMode",
					opts.SessionMode.ToString()
				}
			});
			await SaveCookies();
			await using AppDbContext db = new AppDbContext();
			Account account = await db.Accounts.FindAsync(accountId);
			INotifyPropertyChanged currentAccount = CurrentAccount;
			if (currentAccount != null && _accountPropertyChangedHandler != null)
			{
				try
				{
					currentAccount.PropertyChanged -= _accountPropertyChangedHandler;
				}
				catch
				{
				}
			}
			CurrentAccount = account ?? throw new InvalidOperationException("Account not found");
			AttachToCurrentAccountPropertyChanged();
			string previousUri = browser.Source?.ToString() ?? "https://pwonline.ru";
			if (opts.CreateFreshSession)
			{
				try
				{
					await browser.CreateNewSessionAsync(opts.SessionMode);
				}
				catch
				{
				}
				try
				{
					FingerprintProfile profile = await LoadOrCreateFingerprintAsync(CurrentAccount.Id);
					await browser.ApplyAntiDetectAsync(profile);
				}
				catch
				{
				}
			}
			else if (EnsureNewSessionBeforeSwitchAsync != null)
			{
				try
				{
					await EnsureNewSessionBeforeSwitchAsync();
				}
				catch
				{
				}
			}
			await EnsureBrowserReadyAsync();
			try
			{
				_log.Debug("ChangeAccount(v1): browser ready");
			}
			catch
			{
			}
			Cookie[] cookies = (File.Exists(GetCookieFilePath()) ? (JsonSerializer.Deserialize<Cookie[]>(await File.ReadAllTextAsync(GetCookieFilePath()), JsonSerializerOptions.Web) ?? Array.Empty<Cookie>()) : Array.Empty<Cookie>());
			await Task.Delay(3000);
			await browser.SetCookieAsync(cookies);
			await browser.NavigateAsync(previousUri);
			await browser.ReloadAsync();
			if (!(await browser.WaitForElementExistsAsync(".main_menu", 30000)))
			{
				try
				{
					await browser.NavigateAsync("https://pwonline.ru/");
				}
				catch
				{
				}
				if (!(await browser.WaitForElementExistsAsync(".main_menu", 15000)))
				{
					throw new InvalidOperationException("Page did not load in time (after retry)");
				}
			}
			bool isAuthorized = await IsAuthorizedAsync();
			if (!isAuthorized)
			{
				try
				{
					await browser.NavigateAsync("https://pwonline.ru/");
				}
				catch
				{
				}
				await Task.Delay(300);
				if (await browser.WaitForElementExistsAsync(".main_menu", 10000))
				{
					isAuthorized = await IsAuthorizedAsync();
				}
				if (!isAuthorized)
				{
					return;
				}
			}
			await SaveCookies();
			account.LastVisit = DateTime.UtcNow;
			db.Update(account);
			await db.SaveChangesAsync();
			try
			{
				this.CurrentAccountChanged?.Invoke(account);
			}
			catch
			{
			}
		}
		finally
		{
			SetChanging(value: false);
			_semaphoreSlim.Release();
		}
	}

	private void SetChanging(bool value)
	{
		_isChanging = value;
		try
		{
			this.CurrentAccountChanging?.Invoke(value);
		}
		catch
		{
		}
	}

	private async Task EnsureBrowserReadyAsync()
	{
		try
		{
			await browser.ExecuteScriptAsync("(function(){return 'ok';})()");
		}
		catch
		{
		}
	}

	public async Task<Account[]> GetAccountsAsync()
	{
		Account[] result;
		await using (AppDbContext appDbContext = new AppDbContext())
		{
			result = appDbContext.Accounts.Include((Account x) => x.Squad).Include((Account x) => x.Servers).ThenInclude((AccountServer x) => x.Characters)
				.ToArray();
		}
		return result;
	}

	public async Task ChangeAccountAsync(string accountId)
	{
		await _semaphoreSlim.WaitAsync();
		SetChanging(value: true);
		try
		{
			await SaveCookies();
			await using AppDbContext db = new AppDbContext();
			Account account = await db.Accounts.FindAsync(accountId);
			INotifyPropertyChanged currentAccount = CurrentAccount;
			if (currentAccount != null && _accountPropertyChangedHandler != null)
			{
				try
				{
					currentAccount.PropertyChanged -= _accountPropertyChangedHandler;
				}
				catch
				{
				}
			}
			CurrentAccount = account ?? throw new InvalidOperationException("Account not found");
			AttachToCurrentAccountPropertyChanged();
			string previousUri = browser.Source?.ToString() ?? "https://pwonline.ru";
			await browser.CreateNewSessionAsync(BrowserSessionIsolationMode.SeparateProfile, CurrentAccount.Id);
			FingerprintProfile profile = await LoadOrCreateFingerprintAsync(CurrentAccount.Id);
			await browser.ApplyAntiDetectAsync(profile);
			await EnsureBrowserReadyAsync();
			Cookie[] cookieAsync = (File.Exists(GetCookieFilePath()) ? (JsonSerializer.Deserialize<Cookie[]>(await File.ReadAllTextAsync(GetCookieFilePath()), JsonSerializerOptions.Web) ?? Array.Empty<Cookie>()) : Array.Empty<Cookie>());
			Cookie[] profileCookies = await browser.GetCookiesAsync();
			if ((browser.CurrentProfileWasCreated || profileCookies.Length == 0) && cookieAsync.Length > 0)
			{
				await browser.ImportCookiesAsync(cookieAsync);
			}
			await browser.NavigateAsync(previousUri);
			if (!(await browser.WaitForElementExistsAsync(".main_menu", 30000)))
			{
				try
				{
					await browser.NavigateAsync("https://pwonline.ru/");
				}
				catch
				{
				}
				if (!(await browser.WaitForElementExistsAsync(".main_menu", 15000)))
				{
					throw new InvalidOperationException("Page did not load in time (after retry)");
				}
			}
			bool isAuthorized = await IsAuthorizedAsync();
			if (!isAuthorized)
			{
				try
				{
					await browser.NavigateAsync("https://pwonline.ru/");
				}
				catch
				{
				}
				await Task.Delay(300);
				if (await browser.WaitForElementExistsAsync(".main_menu", 10000))
				{
					isAuthorized = await IsAuthorizedAsync();
				}
				if (!isAuthorized)
				{
					return;
				}
			}
			await SaveCookies();
			account.LastVisit = DateTime.UtcNow;
			db.Update(account);
			await db.SaveChangesAsync();
			try
			{
				this.CurrentAccountChanged?.Invoke(account);
			}
			catch
			{
			}
		}
		finally
		{
			SetChanging(value: false);
			_semaphoreSlim.Release();
		}
	}

	public async Task<bool> IsAuthorizedAsync()
	{
		if (!(await browser.WaitForElementExistsAsync(".auth_h > h2 > a > strong", 500)))
		{
			return false;
		}
		string text = await GetSiteId();
		if (string.IsNullOrWhiteSpace(text))
		{
			await Task.Delay(200);
			text = await GetSiteId();
		}
		if (text != CurrentAccount.SiteId)
		{
			return false;
		}
		return true;
	}

	public async Task ImportBrowserSessionAsync(Cookie[] cookies)
	{
		await _semaphoreSlim.WaitAsync();
		SetChanging(value: true);
		try
		{
			if (CurrentAccount == null)
			{
				throw new InvalidOperationException("Select an account in PW Hub before importing a browser session.");
			}
			Cookie[] allowedCookies = (cookies ?? Array.Empty<Cookie>())
				.Where((Cookie cookie) => IsAllowedCookieDomain(cookie.Domain))
				.ToArray();
			if (allowedCookies.Length == 0)
			{
				throw new InvalidOperationException("The browser did not provide any PW authorization cookies.");
			}
			Cookie[] previousCookies = await browser.GetCookiesAsync();
			try
			{
				await browser.ImportCookiesAsync(allowedCookies);
				await browser.NavigateAsync("https://pwonline.ru/");
				if (!(await browser.WaitForElementExistsAsync(".main_menu", 30000)))
				{
					throw new InvalidOperationException("PW Online did not finish loading after the session import.");
				}
				if (!(await browser.WaitForElementExistsAsync(".auth_h > h2 > a > strong", 10000)))
				{
					throw new InvalidOperationException("The imported browser session is not authorized on PW Online.");
				}
				string siteId = await GetSiteId();
				if (string.IsNullOrWhiteSpace(siteId))
				{
					throw new InvalidOperationException("The imported browser session is not authorized on PW Online.");
				}
				if (!string.IsNullOrWhiteSpace(CurrentAccount.SiteId)
					&& !string.Equals(CurrentAccount.SiteId, siteId, StringComparison.Ordinal))
				{
					throw new InvalidOperationException("The imported session belongs to a different PW Online account.");
				}
				await using AppDbContext db = new AppDbContext();
				Account account = await db.Accounts.FindAsync(CurrentAccount.Id)
					?? throw new InvalidOperationException("The selected account no longer exists.");
				if (string.IsNullOrWhiteSpace(account.SiteId))
				{
					account.SiteId = siteId;
					CurrentAccount.SiteId = siteId;
				}
				account.LastVisit = DateTime.UtcNow;
				CurrentAccount.LastVisit = account.LastVisit;
				await db.SaveChangesAsync();
				await SaveCookies();
				CurrentAccountChanged?.Invoke(CurrentAccount);
			}
			catch
			{
				try
				{
					await browser.SetCookieAsync(previousCookies);
					await browser.ReloadAsync();
				}
				catch
				{
				}
				throw;
			}
		}
		finally
		{
			SetChanging(value: false);
			_semaphoreSlim.Release();
		}
	}

	public async Task SaveCurrentSessionAsync()
	{
		await _semaphoreSlim.WaitAsync();
		try
		{
			await SaveCookies();
		}
		finally
		{
			_semaphoreSlim.Release();
		}
	}

	public async Task<Account> GetAccountAsync()
	{
		return CurrentAccount;
	}

	public async Task<string> GetSiteId()
	{
		return (await browser.ExecuteScriptAsync("$('.auth_h > h2 > a > strong')[0].innerText")).Trim('"').Replace("null", "");
	}

	private void AttachToCurrentAccountPropertyChanged()
	{
		INotifyPropertyChanged currentAccount = CurrentAccount;
		if (currentAccount == null)
		{
			return;
		}
		if (_accountPropertyChangedHandler == null)
		{
			_accountPropertyChangedHandler = delegate
			{
				try
				{
					this.CurrentAccountDataChanged?.Invoke(CurrentAccount);
				}
				catch
				{
				}
			};
		}
		try
		{
			currentAccount.PropertyChanged += _accountPropertyChangedHandler;
		}
		catch
		{
		}
	}

	private string GetCookieFilePath()
	{
		string result = Path.Combine("Cookies", $"{CurrentAccount.Id:N}{".json"}");
		if (!Directory.Exists("Cookies"))
		{
			Directory.CreateDirectory("Cookies");
		}
		return result;
	}

	private string GetFingerprintFilePath(string accountId)
	{
		string text = (string.IsNullOrWhiteSpace(accountId) ? "unknown" : accountId.Replace(":", "_").Replace("/", "_"));
		string result = Path.Combine("Fingerprints", text + ".fp.json");
		if (!Directory.Exists("Fingerprints"))
		{
			Directory.CreateDirectory("Fingerprints");
		}
		return result;
	}

	private async Task<FingerprintProfile> LoadOrCreateFingerprintAsync(string accountId)
	{
		_ = 1;
		try
		{
			string fingerprintFilePath = GetFingerprintFilePath(accountId);
			if (File.Exists(fingerprintFilePath))
			{
				try
				{
					FingerprintProfile fingerprintProfile = JsonSerializer.Deserialize<FingerprintProfile>(await File.ReadAllTextAsync(fingerprintFilePath), JsonSerializerOptions.Web);
					if (fingerprintProfile != null)
					{
						return fingerprintProfile;
					}
				}
				catch
				{
				}
			}
			FingerprintProfile created = FingerprintGenerator.Generate();
			try
			{
				string contents = JsonSerializer.Serialize(created, JsonSerializerOptions.Web);
				await File.WriteAllTextAsync(GetFingerprintFilePath(accountId), contents);
			}
			catch
			{
			}
			return created;
		}
		catch
		{
			return FingerprintGenerator.Generate();
		}
	}

	private async Task SaveCookies()
	{
		if (CurrentAccount != null && !(await GetSiteId() != CurrentAccount.SiteId))
		{
			string contents = JsonSerializer.Serialize(await browser.GetCookiesAsync(), JsonSerializerOptions.Web);
			await File.WriteAllTextAsync(GetCookieFilePath(), contents);
		}
	}

	private static bool IsAllowedCookieDomain(string? domain)
	{
		string value = (domain ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
		return value == "pwonline.ru" || value.EndsWith(".pwonline.ru", StringComparison.Ordinal)
			|| value == "pw.mail.ru" || value.EndsWith(".pw.mail.ru", StringComparison.Ordinal);
	}
}

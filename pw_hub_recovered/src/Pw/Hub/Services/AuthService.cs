using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Abstractions;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public class AuthService
{
	private readonly DispatcherTimer _dispatcherTimer;

	private bool _running;

	public AuthService()
	{
		_dispatcherTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMinutes(1.0)
		};
		_dispatcherTimer.Tick += DispatcherTimerOnTick;
		_dispatcherTimer.Start();
	}

	public async Task CheckAccounts(bool ignoreVisibility = false)
	{
		if (_running)
		{
			return;
		}
		_running = true;
		try
		{
			Window mainWindow = System.Windows.Application.Current.MainWindow;
			if (!(mainWindow is MainWindow mainWindow2))
			{
				return;
			}
			System.Windows.Application current = System.Windows.Application.Current;
			if (!(current is App app) || (!ignoreVisibility && mainWindow2.IsVisible))
			{
				return;
			}
			DateTime threshold = DateTime.UtcNow.AddDays(-1.0);
			await using AppDbContext db = new AppDbContext();
			List<Account> list = await db.Accounts.Where((Account x) => x.LastVisit <= threshold).AsNoTracking().ToListAsync();
			if (list.Count == 0)
			{
				return;
			}
			List<Account> notAuthorized = new List<Account>();
			List<Account> reAuthorized = new List<Account>();
			IAccountManager manager = mainWindow2.AccountPage.AccountManager;
			foreach (Account account in list)
			{
				await manager.ChangeAccountAsync(account.Id);
				if (await manager.IsAuthorizedAsync())
				{
					reAuthorized.Add(account);
				}
				else
				{
					notAuthorized.Add(account);
				}
			}
			app.NotifyIcon.ShowBalloonTip(5, "Authorization Check", $"Signed out: {notAuthorized.Count}, signed back in: {reAuthorized.Count}", (notAuthorized.Count <= 0) ? ToolTipIcon.Info : ToolTipIcon.Warning);
		}
		finally
		{
			_running = false;
		}
	}

	private async void DispatcherTimerOnTick(object sender, EventArgs e)
	{
		await CheckAccounts();
	}
}

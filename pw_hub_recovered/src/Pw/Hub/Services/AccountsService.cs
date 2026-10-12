using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public class AccountsService : IAccountsService
{
	public List<Squad> LoadSquads()
	{
		using AppDbContext appDbContext = new AppDbContext();
		appDbContext.Database.Migrate();
		List<Squad> list = (from s in appDbContext.Squads.Include((Squad s) => s.Accounts).ThenInclude((Account a) => a.Servers).ThenInclude((AccountServer sv) => sv.Characters)
			orderby s.OrderIndex, s.Name
			select s).ToList();
		List<Squad> list2 = new List<Squad>(list.Count);
		foreach (Squad item in list)
		{
			List<Account> list3 = (from a in item.Accounts
				orderby a.OrderIndex, a.Name
				select a).ToList();
			list2.Add(new Squad
			{
				Id = item.Id,
				Name = item.Name,
				OrderIndex = item.OrderIndex,
				Accounts = new ObservableCollection<Account>(list3)
			});
		}
		return list2;
	}

	public async Task<Squad> CreateSquadAsync(string name)
	{
		Squad result;
		await using (AppDbContext db = new AppDbContext())
		{
			int num = (await ((IQueryable<Squad>)db.Squads).Select((Expression<Func<Squad, int?>>)((Squad x) => x.OrderIndex)).MaxAsync()) ?? (-1);
			Squad entity = new Squad
			{
				Name = name,
				OrderIndex = num + 1
			};
			db.Squads.Add(entity);
			await db.SaveChangesAsync();
			result = entity;
		}
		return result;
	}

	public async Task UpdateSquadAsync(Squad squad, string newName)
	{
		if (squad == null)
		{
			return;
		}
		await using AppDbContext db = new AppDbContext();
		Squad squad2 = await db.Squads.FirstOrDefaultAsync((Squad s) => s.Id == squad.Id);
		if (squad2 != null)
		{
			squad2.Name = newName;
			db.Update(squad2);
			await db.SaveChangesAsync();
		}
	}

	public async Task DeleteSquadAsync(Squad squad)
	{
		if (squad == null)
		{
			return;
		}
		await using AppDbContext db = new AppDbContext();
		Squad squad2 = await db.Squads.Include((Squad s) => s.Accounts).FirstOrDefaultAsync((Squad s) => s.Id == squad.Id);
		if (squad2 != null)
		{
			db.Squads.Remove(squad2);
			await db.SaveChangesAsync();
		}
	}

	public async Task<Account> CreateAccountAsync(Squad squad, string accountName)
	{
		Account result;
		await using (AppDbContext db = new AppDbContext())
		{
			int num = (await db.Accounts.Where((Account a) => a.SquadId == squad.Id).Select((Expression<Func<Account, int?>>)((Account a) => a.OrderIndex)).MaxAsync()) ?? (-1);
			Account account = new Account
			{
				Name = accountName,
				SquadId = squad.Id,
				OrderIndex = num + 1,
				ImageSource = string.Empty
			};
			db.Accounts.Add(account);
			await db.SaveChangesAsync();
			result = account;
		}
		return result;
	}

	public async Task UpdateAccountAsync(Account account, string newName)
	{
		if (account == null)
		{
			return;
		}
		await using AppDbContext db = new AppDbContext();
		Account account2 = await db.Accounts.Include((Account a) => a.Servers).FirstOrDefaultAsync((Account a) => a.Id == account.Id);
		if (account2 == null)
		{
			return;
		}
		account2.Name = newName;
		if (account2.Servers != null && account2.Servers.Count > 0)
		{
			List<AccountServer> source = account.Servers ?? new List<AccountServer>();
			foreach (AccountServer dbServer in account2.Servers)
			{
				AccountServer accountServer = source.FirstOrDefault((AccountServer s) => (!string.IsNullOrEmpty(dbServer.Id) && s.Id == dbServer.Id) || (!string.IsNullOrEmpty(dbServer.OptionId) && s.OptionId == dbServer.OptionId));
				if (accountServer != null)
				{
					dbServer.DefaultCharacterOptionId = (string.IsNullOrWhiteSpace(accountServer.DefaultCharacterOptionId) ? null : accountServer.DefaultCharacterOptionId);
				}
			}
		}
		db.Update(account2);
		await db.SaveChangesAsync();
	}

	public async Task DeleteAccountAsync(Account account)
	{
		if (account == null)
		{
			return;
		}
		await using AppDbContext db = new AppDbContext();
		Account account2 = await db.Accounts.FirstOrDefaultAsync((Account a) => a.Id == account.Id);
		if (account2 != null)
		{
			db.Accounts.Remove(account2);
			await db.SaveChangesAsync();
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public class OrderingService : IOrderingService
{
	public async Task ReorderSquadAsync(Squad moved, Squad target, bool insertAfter)
	{
		if (moved == null || target == null || moved.Id == target.Id)
		{
			return;
		}
		await using AppDbContext db = new AppDbContext();
		List<Squad> list = await (from s in db.Squads
			orderby s.OrderIndex, s.Name
			select s).ToListAsync();
		int num = list.FindIndex((Squad s) => s.Id == moved.Id);
		if (num < 0)
		{
			return;
		}
		list.RemoveAt(num);
		int num2 = list.FindIndex((Squad s) => s.Id == target.Id);
		if (num2 < 0)
		{
			return;
		}
		int num3 = (insertAfter ? (num2 + 1) : num2);
		if (num3 < 0)
		{
			num3 = 0;
		}
		if (num3 > list.Count)
		{
			num3 = list.Count;
		}
		list.Insert(num3, moved);
		for (int num4 = 0; num4 < list.Count; num4++)
		{
			Squad squad = list[num4];
			if (squad.OrderIndex != num4)
			{
				squad.OrderIndex = num4;
				db.Update(squad);
			}
		}
		await db.SaveChangesAsync();
	}

	public async Task ReorderAccountAsync(Account moved, Squad targetSquad, Account? targetAccount, bool insertAfter)
	{
		if (moved == null || targetSquad == null)
		{
			return;
		}
		await using AppDbContext db = new AppDbContext();
		Squad squad = await db.Squads.Include((Squad s) => s.Accounts).FirstOrDefaultAsync((Squad s) => s.Id == targetSquad.Id);
		if (squad == null)
		{
			return;
		}
		if (!string.Equals(moved.SquadId, squad.Id, StringComparison.Ordinal))
		{
			Account account = await db.Accounts.FirstOrDefaultAsync((Account a) => a.Id == moved.Id);
			if (account == null)
			{
				return;
			}
			account.SquadId = squad.Id;
			await db.SaveChangesAsync();
		}
		await db.Entry(squad).Collection((Squad s) => s.Accounts).LoadAsync();
		List<Account> accounts = (from a in squad.Accounts
			orderby a.OrderIndex, a.Name
			select a).ToList();
		accounts.RemoveAll((Account a) => a.Id == moved.Id);
		int insertIdx;
		if (targetAccount == null)
		{
			insertIdx = accounts.Count;
		}
		else
		{
			int num = accounts.FindIndex((Account a) => a.Id == targetAccount.Id);
			insertIdx = ((num < 0) ? accounts.Count : (insertAfter ? (num + 1) : num));
		}
		Account account2 = await db.Accounts.FirstOrDefaultAsync((Account a) => a.Id == moved.Id);
		if (account2 == null)
		{
			return;
		}
		account2.Name = account2.Name;
		account2.SquadId = squad.Id;
		accounts.Insert(insertIdx, account2);
		for (int num2 = 0; num2 < accounts.Count; num2++)
		{
			Account account3 = accounts[num2];
			if (account3.OrderIndex != num2)
			{
				account3.OrderIndex = num2;
				db.Update(account3);
			}
		}
		await db.SaveChangesAsync();
	}
}

using System;
using System.Threading.Tasks;
using Pw.Hub.Models;

namespace Pw.Hub.Abstractions;

public interface IAccountManager
{
	Account CurrentAccount { get; }

	bool IsChanging { get; }

	event Action<Account> CurrentAccountChanged;

	event Action<Account> CurrentAccountDataChanged;

	event Action<bool> CurrentAccountChanging;

	Task<Account[]> GetAccountsAsync();

	Task ChangeAccountAsync(string accountId);

	Task ImportBrowserSessionAsync(Cookie[] cookies);

	Task SaveCurrentSessionAsync();

	Task<bool> IsAuthorizedAsync();

	Task<Account> GetAccountAsync();

	Task<string> GetSiteId();
}

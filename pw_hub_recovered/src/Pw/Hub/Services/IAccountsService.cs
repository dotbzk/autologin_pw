using System.Collections.Generic;
using System.Threading.Tasks;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public interface IAccountsService
{
	List<Squad> LoadSquads();

	Task<Squad> CreateSquadAsync(string name);

	Task UpdateSquadAsync(Squad squad, string newName);

	Task DeleteSquadAsync(Squad squad);

	Task<Account> CreateAccountAsync(Squad squad, string accountName);

	Task UpdateAccountAsync(Account account, string newName);

	Task DeleteAccountAsync(Account account);
}

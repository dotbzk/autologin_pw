using System.Threading.Tasks;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public interface IOrderingService
{
	Task ReorderSquadAsync(Squad moved, Squad target, bool insertAfter);

	Task ReorderAccountAsync(Account moved, Squad targetSquad, Account? targetAccount, bool insertAfter);
}

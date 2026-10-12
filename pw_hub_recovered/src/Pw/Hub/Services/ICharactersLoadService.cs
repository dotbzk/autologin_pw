using System.Threading.Tasks;
using System.Windows;
using Pw.Hub.Models;
using Pw.Hub.Pages;

namespace Pw.Hub.Services;

public interface ICharactersLoadService
{
	Task LoadForAccountAsync(Account account, AccountPage accountPage, Window owner);
}

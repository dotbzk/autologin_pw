using System;
using System.Threading.Tasks;

namespace Pw.Hub.Services;

public interface IModulesSyncService
{
	Task<bool> SyncInstalledAsync();

	void InstallModuleLocally(ModuleDto module);

	void RemoveModuleLocally(Guid id);
}

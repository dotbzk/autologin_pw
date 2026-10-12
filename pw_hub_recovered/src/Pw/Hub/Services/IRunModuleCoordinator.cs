using System.Threading.Tasks;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public interface IRunModuleCoordinator
{
	Task RunAsync(ModuleDefinition module);
}

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Pw.Hub.Models;
using Pw.Hub.Tools;

namespace Pw.Hub.Services;

public interface ILuaExecutionService
{
	Task RunAsync(LuaScriptRunner runner, ModuleDefinition module, Dictionary<string, object> args, Window owner);
}

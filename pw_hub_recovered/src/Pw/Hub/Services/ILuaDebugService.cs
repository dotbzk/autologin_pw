using System.Collections.Generic;
using System.Threading.Tasks;
using Pw.Hub.Tools;

namespace Pw.Hub.Services;

public interface ILuaDebugService
{
	Task RunWithBreakpointsAsync(LuaScriptRunner runner, string code, ISet<int> breakpoints, LuaScriptRunner.DebugBreakHandler onBreak, Dictionary<string, object> args);
}

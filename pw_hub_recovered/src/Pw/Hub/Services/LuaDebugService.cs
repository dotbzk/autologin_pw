using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pw.Hub.Tools;

namespace Pw.Hub.Services;

public class LuaDebugService : ILuaDebugService
{
	public async Task RunWithBreakpointsAsync(LuaScriptRunner runner, string code, ISet<int> breakpoints, LuaScriptRunner.DebugBreakHandler onBreak, Dictionary<string, object> args)
	{
		if (runner == null)
		{
			throw new ArgumentNullException("runner");
		}
		if (code == null)
		{
			code = string.Empty;
		}
		if (breakpoints == null)
		{
			breakpoints = new HashSet<int>();
		}
		if (args == null)
		{
			args = new Dictionary<string, object>();
		}
		await runner.RunCodeWithBreakpointsAsync(code, breakpoints, onBreak, args);
	}
}

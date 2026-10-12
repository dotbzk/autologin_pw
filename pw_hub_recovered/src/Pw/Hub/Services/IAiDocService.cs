using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pw.Hub.Services;

public interface IAiDocService
{
	Task<string> GenerateDescriptionAsync(string name, string version, IReadOnlyCollection<(string Name, string Type, string Label)> inputs, string scriptFragment, string? previousDescription = null, string? previousScriptFragment = null, CancellationToken ct = default(CancellationToken));
}

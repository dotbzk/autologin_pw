using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pw.Hub.Services;

public interface IAiAssistantService
{
	Task<AiAssistantResponse> SendAsync(string prompt, string currentCode, string? manualChangesDiff = null, CancellationToken ct = default(CancellationToken), Action<string>? onStreamDelta = null);

	void NewSession();
}

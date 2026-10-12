using System.Collections.Generic;

namespace Pw.Hub.Services;

public sealed class AiAssistantResponse
{
	public string AssistantText { get; set; } = string.Empty;

	public string Summary { get; set; } = string.Empty;

	public string ExtractedCode { get; set; } = string.Empty;

	public IList<string> DiffLines { get; set; } = new List<string>();
}

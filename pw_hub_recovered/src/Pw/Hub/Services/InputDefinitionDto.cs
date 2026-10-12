using System;

namespace Pw.Hub.Services;

public class InputDefinitionDto
{
	public string Name { get; set; } = string.Empty;

	public string Label { get; set; } = string.Empty;

	public string Type { get; set; } = string.Empty;

	public string Default { get; set; } = string.Empty;

	public bool Required { get; set; }

	public string[] Options { get; set; } = Array.Empty<string>();
}

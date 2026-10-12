using System;

namespace Pw.Hub.Services;

public class CreateOrUpdateModule
{
	public string Name { get; set; } = string.Empty;

	public string Version { get; set; } = "1.0.0";

	public string Description { get; set; }

	public string Script { get; set; } = string.Empty;

	public InputDefinitionDto[] Inputs { get; set; } = Array.Empty<InputDefinitionDto>();
}

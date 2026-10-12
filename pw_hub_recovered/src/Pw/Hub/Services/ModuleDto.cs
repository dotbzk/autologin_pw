using System;

namespace Pw.Hub.Services;

public class ModuleDto
{
	public Guid Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public string Version { get; set; } = "1.0.0";

	public string Description { get; set; }

	public string DescriptionHtml { get; set; } = string.Empty;

	public string Script { get; set; } = string.Empty;

	public InputDefinitionDto[] Inputs { get; set; } = Array.Empty<InputDefinitionDto>();

	public long RunCount { get; set; }

	public int InstallCount { get; set; }

	public DateTimeOffset CreatedAt { get; set; }

	public DateTimeOffset UpdatedAt { get; set; }

	public string OwnerUserId { get; set; }

	public string AuthorUsername { get; set; }
}

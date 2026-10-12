using System.Collections.Generic;

namespace Pw.Hub.Models;

public class ModuleDefinition
{
	public string Id { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string Version { get; set; } = "1.0.0";

	public string Description { get; set; } = string.Empty;

	public string Script { get; set; } = string.Empty;

	public List<ModuleInput> Inputs { get; set; } = new List<ModuleInput>();

	public Dictionary<string, string> LastArgs { get; set; } = new Dictionary<string, string>();
}

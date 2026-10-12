using System.Collections.Generic;

namespace Pw.Hub.Models;

public class ModuleInput
{
	public string Name { get; set; } = string.Empty;

	public string Label { get; set; } = string.Empty;

	public string Type { get; set; } = "string";

	public string Default { get; set; }

	public bool Required { get; set; }

	public List<string> Options { get; set; } = new List<string>();
}

using System.Collections.Generic;

namespace Pw.Hub.Models;

public class ModuleManifest
{
	public List<ModuleDefinition> Modules { get; set; } = new List<ModuleDefinition>();
}

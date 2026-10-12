using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public class ModuleService
{
	public string ManifestPath { get; }

	public ModuleService()
	{
		string baseDirectory = AppContext.BaseDirectory;
		ManifestPath = Path.Combine(baseDirectory, "modules.json");
	}

	public List<ModuleDefinition> LoadModules()
	{
		try
		{
			if (!File.Exists(ManifestPath))
			{
				return new List<ModuleDefinition>();
			}
			string text = File.ReadAllText(ManifestPath);
			JsonSerializerOptions options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};
			if (text.TrimStart().StartsWith("["))
			{
				List<ModuleDefinition> modules = JsonSerializer.Deserialize<List<ModuleDefinition>>(text, options) ?? new List<ModuleDefinition>();
				return Normalize(modules);
			}
			ModuleManifest moduleManifest = JsonSerializer.Deserialize<ModuleManifest>(text, options) ?? new ModuleManifest();
			return Normalize(moduleManifest.Modules);
		}
		catch
		{
			return new List<ModuleDefinition>();
		}
	}

	public void SaveModules(List<ModuleDefinition> modules)
	{
		try
		{
			string contents = JsonSerializer.Serialize(new ModuleManifest
			{
				Modules = Normalize(modules)
			}, new JsonSerializerOptions
			{
				WriteIndented = true,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			});
			File.WriteAllText(ManifestPath, contents);
		}
		catch
		{
		}
	}

	public void AddOrUpdateModule(ModuleDefinition module)
	{
		List<ModuleDefinition> list = LoadModules();
		ModuleDefinition moduleDefinition = list.FirstOrDefault((ModuleDefinition m) => string.Equals(m.Id, module.Id, StringComparison.OrdinalIgnoreCase));
		if (moduleDefinition == null)
		{
			list.Add(module);
		}
		else
		{
			moduleDefinition.Name = module.Name;
			moduleDefinition.Version = (string.IsNullOrWhiteSpace(module.Version) ? moduleDefinition.Version : module.Version);
			moduleDefinition.Description = module.Description;
			moduleDefinition.Script = module.Script;
			moduleDefinition.Inputs = module.Inputs ?? new List<ModuleInput>();
			moduleDefinition.LastArgs = module.LastArgs ?? moduleDefinition.LastArgs ?? new Dictionary<string, string>();
		}
		SaveModules(list);
	}

	private List<ModuleDefinition> Normalize(List<ModuleDefinition> modules)
	{
		foreach (ModuleDefinition module in modules)
		{
			module.Id = ((!string.IsNullOrWhiteSpace(module.Id)) ? module.Id : (module.Name?.Replace(' ', '_') ?? Guid.NewGuid().ToString("N")));
			ModuleDefinition moduleDefinition = module;
			if (moduleDefinition.Inputs == null)
			{
				List<ModuleInput> list = (moduleDefinition.Inputs = new List<ModuleInput>());
			}
			foreach (ModuleInput input in module.Inputs)
			{
				if (string.IsNullOrWhiteSpace(input.Label))
				{
					input.Label = input.Name;
				}
				if (string.IsNullOrWhiteSpace(input.Type))
				{
					input.Type = "string";
				}
				if (string.IsNullOrWhiteSpace(input.Default))
				{
					input.Default = string.Empty;
				}
				ModuleInput moduleInput = input;
				if (moduleInput.Options == null)
				{
					List<string> list3 = (moduleInput.Options = new List<string>());
				}
			}
		}
		return modules.OrderBy((ModuleDefinition m) => m.Name).ToList();
	}
}

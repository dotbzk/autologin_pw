using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public class ModulesSyncService : IModulesSyncService
{
	private readonly ModuleService _moduleService;

	private readonly ModulesApiClient _modulesApi;

	public ModulesSyncService()
	{
		_moduleService = new ModuleService();
		_modulesApi = new ModulesApiClient();
	}

	public async Task<bool> SyncInstalledAsync()
	{
		_ = 1;
		try
		{
			if (_modulesApi.CurrentUser == null)
			{
				try
				{
					await _modulesApi.MeAsync();
				}
				catch
				{
				}
			}
			string text = _modulesApi.CurrentUser?.UserId;
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			List<ModuleDto> source = await _modulesApi.GetInstalledAsync(text);
			HashSet<Guid> hashSet = new HashSet<Guid>(source.Select((ModuleDto m) => m.Id));
			Guid result2;
			HashSet<Guid> hashSet2 = new HashSet<Guid>(from m in _moduleService.LoadModules()
				select (!Guid.TryParse(m.Id, out result2)) ? Guid.Empty : result2 into g
				where g != Guid.Empty
				select g);
			List<Guid> list = hashSet.Except(hashSet2).ToList();
			List<Guid> list2 = hashSet2.Except(hashSet).ToList();
			bool result = false;
			if (list.Count > 0)
			{
				foreach (Guid id in list)
				{
					ModuleDto moduleDto = source.FirstOrDefault((ModuleDto x) => x.Id == id);
					if (moduleDto != null)
					{
						try
						{
							InstallModuleLocally(moduleDto);
							result = true;
						}
						catch
						{
						}
					}
				}
			}
			if (list2.Count > 0)
			{
				foreach (Guid item in list2)
				{
					try
					{
						RemoveModuleLocally(item);
						result = true;
					}
					catch
					{
					}
				}
			}
			return result;
		}
		catch
		{
			return false;
		}
	}

	public void InstallModuleLocally(ModuleDto module)
	{
		try
		{
			string text = Path.Combine(AppContext.BaseDirectory, "Scripts");
			Directory.CreateDirectory(text);
			string text2 = module.Id.ToString() + ".lua";
			File.WriteAllText(Path.Combine(text, text2), module.Script ?? string.Empty);
			ModuleDefinition module2 = new ModuleDefinition
			{
				Id = module.Id.ToString(),
				Name = module.Name,
				Version = (string.IsNullOrWhiteSpace(module.Version) ? "1.0.0" : module.Version),
				Description = (module.Description ?? string.Empty),
				Script = text2,
				Inputs = (module.Inputs?.Select((InputDefinitionDto i) => new ModuleInput
				{
					Name = i.Name,
					Label = (string.IsNullOrWhiteSpace(i.Label) ? i.Name : i.Label),
					Type = (string.IsNullOrWhiteSpace(i.Type) ? "string" : i.Type),
					Default = (string.IsNullOrWhiteSpace(i.Default) ? null : i.Default),
					Required = i.Required,
					Options = (from s in (i.Options == null) ? ((IEnumerable<string>)Array.Empty<string>()) : ((IEnumerable<string>)i.Options)
						where !string.IsNullOrWhiteSpace(s)
						select s.Trim()).Distinct<string>(StringComparer.Ordinal).ToList()
				}).ToList() ?? new List<ModuleInput>())
			};
			_moduleService.AddOrUpdateModule(module2);
		}
		catch
		{
		}
	}

	public void RemoveModuleLocally(Guid id)
	{
		try
		{
			List<ModuleDefinition> list = _moduleService.LoadModules();
			ModuleDefinition moduleDefinition = list.FirstOrDefault((ModuleDefinition x) => string.Equals(x.Id, id.ToString(), StringComparison.OrdinalIgnoreCase));
			if (moduleDefinition == null)
			{
				return;
			}
			try
			{
				string baseDirectory = AppContext.BaseDirectory;
				string path = Path.Combine(baseDirectory, moduleDefinition.Script);
				string path2 = Path.Combine(baseDirectory, "Scripts", moduleDefinition.Script);
				if (File.Exists(path))
				{
					File.Delete(path);
				}
				else if (File.Exists(path2))
				{
					File.Delete(path2);
				}
			}
			catch
			{
			}
			list.Remove(moduleDefinition);
			_moduleService.SaveModules(list);
		}
		catch
		{
		}
	}
}

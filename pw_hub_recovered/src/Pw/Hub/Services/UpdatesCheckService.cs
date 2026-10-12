using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public class UpdatesCheckService : IUpdatesCheckService
{
	private readonly ModuleService _moduleService;

	private readonly ModulesApiClient _modulesApiClient;

	public UpdatesCheckService()
	{
		_moduleService = new ModuleService();
		_modulesApiClient = new ModulesApiClient();
	}

	public async Task<List<string>> GetUpdatesAsync()
	{
		List<string> result = new List<string>();
		try
		{
			List<ModuleDefinition> locals = _moduleService.LoadModules();
			if (locals == null || locals.Count == 0)
			{
				return result;
			}
			List<ModuleDto> source = (await _modulesApiClient.SearchAsync(null, null, null, null, 1, 100))?.Items ?? new List<ModuleDto>();
			foreach (ModuleDefinition item in locals)
			{
				if (Guid.TryParse(item.Id, out var id))
				{
					ModuleDto moduleDto = source.FirstOrDefault((ModuleDto i) => i.Id == id);
					if (moduleDto != null && IsUpdateAvailable(item.Version ?? "1.0.0", moduleDto.Version ?? "1.0.0"))
					{
						result.Add(item.Name ?? item.Id);
					}
				}
			}
		}
		catch
		{
		}
		return result;
	}

	private static bool IsUpdateAvailable(string localVersion, string remoteVersion)
	{
		try
		{
			int[] array = ParseVersion(localVersion);
			int[] array2 = ParseVersion(remoteVersion);
			for (int i = 0; i < Math.Max(array.Length, array2.Length); i++)
			{
				int num = ((i < array.Length) ? array[i] : 0);
				int num2 = ((i < array2.Length) ? array2[i] : 0);
				if (num != num2)
				{
					return num < num2;
				}
			}
			return false;
		}
		catch
		{
			return !string.Equals(localVersion, remoteVersion, StringComparison.OrdinalIgnoreCase);
		}
	}

	private static int[] ParseVersion(string v)
	{
		int result;
		return (from s in (v ?? "0").Split('.', StringSplitOptions.RemoveEmptyEntries)
			select int.TryParse(s, out result) ? result : 0).ToArray();
	}
}

using System.Collections.Generic;

namespace Pw.Hub.Services;

public class PagedModulesResponse
{
	public int Total { get; set; }

	public int Page { get; set; }

	public int PageSize { get; set; }

	public List<ModuleDto> Items { get; set; } = new List<ModuleDto>();
}

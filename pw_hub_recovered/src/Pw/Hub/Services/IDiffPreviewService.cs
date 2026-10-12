using System.Collections.Generic;

namespace Pw.Hub.Services;

public interface IDiffPreviewService
{
	IList<string> BuildUnifiedDiffGit(string oldText, string newText, int context = 3);
}

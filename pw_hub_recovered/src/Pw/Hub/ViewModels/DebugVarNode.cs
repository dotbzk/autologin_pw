using System.Collections.ObjectModel;

namespace Pw.Hub.ViewModels;

public class DebugVarNode
{
	public string Name { get; set; }

	public string DisplayValue { get; set; }

	public string TypeName { get; set; }

	public ObservableCollection<DebugVarNode> Children { get; } = new ObservableCollection<DebugVarNode>();
}

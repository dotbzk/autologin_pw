namespace Pw.Hub.Windows;

internal sealed class ApiSymbol
{
	public string Name { get; }

	public string Signature { get; }

	public string Description { get; }

	public ApiSymbol(string name, string signature, string description)
	{
		Name = name;
		Signature = signature;
		Description = description;
	}
}

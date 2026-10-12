namespace Pw.Hub.Services;

public class UpdateManifest
{
	public string Version { get; set; } = "1.0.0";

	public string Url { get; set; } = string.Empty;

	public string ReleaseNotes { get; set; }

	public bool Mandatory { get; set; }
}

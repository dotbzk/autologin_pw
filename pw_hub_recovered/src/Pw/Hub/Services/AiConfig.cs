namespace Pw.Hub.Services;

public sealed class AiConfig
{
	public string ApiUrl { get; set; } = "https://ollama.com/api/chat";

	public string Model { get; set; } = "llama3.1";

	public string ApiKey { get; set; } = string.Empty;
}

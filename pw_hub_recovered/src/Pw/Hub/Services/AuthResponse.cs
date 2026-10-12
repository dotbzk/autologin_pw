namespace Pw.Hub.Services;

public class AuthResponse
{
	public string UserId { get; set; } = string.Empty;

	public string Username { get; set; } = string.Empty;

	public bool Developer { get; set; }

	public string Token { get; set; } = string.Empty;
}

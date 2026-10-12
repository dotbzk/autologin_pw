using System;

namespace Pw.Hub.Services;

public class UserDto
{
	public string UserId { get; set; } = string.Empty;

	public string Username { get; set; } = string.Empty;

	public bool Developer { get; set; }

	public long? TelegramId { get; set; }

	public string? TelegramUsername { get; set; }

	public DateTimeOffset? TelegramLinkedAt { get; set; }
}

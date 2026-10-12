using System;
using System.IO;
using System.Text.Json;

namespace Pw.Hub.Services;

public static class AuthState
{
	private class AuthPersisted
	{
		public string Token { get; set; }
	}

	private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pw.Hub");

	private static readonly string FilePath = Path.Combine(Folder, "auth.json");

	public static string Token { get; private set; }

	public static UserDto CurrentUser { get; private set; }

	public static void Load()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				Token = JsonSerializer.Deserialize<AuthPersisted>(File.ReadAllText(FilePath))?.Token;
				CurrentUser = null;
			}
		}
		catch
		{
		}
	}

	public static void Save()
	{
		try
		{
			Directory.CreateDirectory(Folder);
			string contents = JsonSerializer.Serialize(new AuthPersisted
			{
				Token = Token
			}, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(FilePath, contents);
		}
		catch
		{
		}
	}

	public static void Set(string token, UserDto user, bool remember)
	{
		Token = token;
		CurrentUser = user;
		if (remember)
		{
			Save();
		}
	}
}

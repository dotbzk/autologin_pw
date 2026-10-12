using System;
using System.IO;
using System.Text.Json;

namespace Pw.Hub.Services;

public sealed class AiConfigService : IAiConfigService
{
	private readonly JsonSerializerOptions _json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		WriteIndented = true
	};

	private readonly string _configFilePath;

	public AiConfigService()
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pw.Hub");
		try
		{
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
		}
		catch
		{
		}
		_configFilePath = Path.Combine(text, "ai.config.json");
	}

	public AiConfig Load()
	{
		try
		{
			if (!File.Exists(_configFilePath))
			{
				return GetFromEnvOrDefault();
			}
			return Sanitize(JsonSerializer.Deserialize<AiConfig>(File.ReadAllText(_configFilePath), _json) ?? new AiConfig());
		}
		catch
		{
			return GetFromEnvOrDefault();
		}
	}

	public void Save(AiConfig cfg)
	{
		if (cfg == null)
		{
			cfg = new AiConfig();
		}
		cfg = Sanitize(cfg);
		try
		{
			string contents = JsonSerializer.Serialize(cfg, _json);
			File.WriteAllText(_configFilePath, contents);
		}
		catch
		{
		}
		try
		{
			if (!string.IsNullOrWhiteSpace(cfg.ApiUrl))
			{
				Environment.SetEnvironmentVariable("OLLAMA_CHAT_URL", cfg.ApiUrl, EnvironmentVariableTarget.User);
				Environment.SetEnvironmentVariable("OLLAMA_CHAT_URL", cfg.ApiUrl, EnvironmentVariableTarget.Process);
			}
			if (!string.IsNullOrWhiteSpace(cfg.Model))
			{
				Environment.SetEnvironmentVariable("OLLAMA_MODEL", cfg.Model, EnvironmentVariableTarget.User);
				Environment.SetEnvironmentVariable("OLLAMA_MODEL", cfg.Model, EnvironmentVariableTarget.Process);
			}
			Environment.SetEnvironmentVariable("OLLAMA_API_KEY", cfg.ApiKey ?? string.Empty, EnvironmentVariableTarget.User);
			Environment.SetEnvironmentVariable("OLLAMA_API_KEY", cfg.ApiKey ?? string.Empty, EnvironmentVariableTarget.Process);
		}
		catch
		{
		}
	}

	public AiConfig GetEffective()
	{
		AiConfig aiConfig = LoadFromFileOrNull();
		if (aiConfig != null)
		{
			return aiConfig;
		}
		return GetFromEnvOrDefault();
	}

	private AiConfig? LoadFromFileOrNull()
	{
		try
		{
			if (!File.Exists(_configFilePath))
			{
				return null;
			}
			AiConfig aiConfig = JsonSerializer.Deserialize<AiConfig>(File.ReadAllText(_configFilePath), _json);
			return (aiConfig == null) ? null : Sanitize(aiConfig);
		}
		catch
		{
			return null;
		}
	}

	private static AiConfig GetFromEnvOrDefault()
	{
		string text = (Environment.GetEnvironmentVariable("OLLAMA_CHAT_URL") ?? string.Empty).Trim();
		string text2 = (Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? string.Empty).Trim();
		string text3 = (Environment.GetEnvironmentVariable("OLLAMA_API_KEY") ?? string.Empty).Trim();
		return new AiConfig
		{
			ApiUrl = (string.IsNullOrWhiteSpace(text) ? "https://ollama.com/api/chat" : text),
			Model = (string.IsNullOrWhiteSpace(text2) ? "llama3.1" : text2),
			ApiKey = (text3 ?? string.Empty)
		};
	}

	private static AiConfig Sanitize(AiConfig cfg)
	{
		cfg.ApiUrl = (cfg.ApiUrl ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(cfg.ApiUrl))
		{
			cfg.ApiUrl = "https://ollama.com/api/chat";
		}
		cfg.Model = (cfg.Model ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(cfg.Model))
		{
			cfg.Model = "llama3.1";
		}
		cfg.ApiKey = (cfg.ApiKey ?? string.Empty).Trim();
		return cfg;
	}
}

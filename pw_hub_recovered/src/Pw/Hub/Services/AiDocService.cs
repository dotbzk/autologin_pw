using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Pw.Hub.Services;

public sealed class AiDocService : IAiDocService, IDisposable
{
	private sealed class ChatMessage
	{
		public string role { get; set; } = string.Empty;

		public string content { get; set; } = string.Empty;
	}

	private sealed class ChatRequest
	{
		public string model { get; set; } = string.Empty;

		public List<ChatMessage> messages { get; set; } = new List<ChatMessage>();

		public bool stream { get; set; }
	}

	private sealed class ChatResponse
	{
		public List<ChatMessage> messages { get; set; } = new List<ChatMessage>();
	}

	private sealed class StreamChunk
	{
		public bool done { get; set; }

		public ChatMessage message { get; set; }

		public string response { get; set; }
	}

	private readonly HttpClient _http;

	private readonly JsonSerializerOptions _json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private readonly string _apiUrl;

	private readonly string _apiKey;

	private readonly string _model;

	private readonly IAiConfigService _cfg;

	public AiDocService()
	{
		_http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(60.0)
		};
		_cfg = (App.Services?.GetService(typeof(IAiConfigService)) as IAiConfigService) ?? new AiConfigService();
		AiConfig effective = _cfg.GetEffective();
		_apiUrl = effective.ApiUrl;
		_apiKey = effective.ApiKey;
		_model = effective.Model;
	}

	public async Task<string> GenerateDescriptionAsync(string name, string version, IReadOnlyCollection<(string Name, string Type, string Label)> inputs, string scriptFragment, string? previousDescription = null, string? previousScriptFragment = null, CancellationToken ct = default(CancellationToken))
	{
		string requestUri = _apiUrl;
		string text = _apiKey;
		string model = _model;
		try
		{
			string text2 = Environment.GetEnvironmentVariable("OLLAMA_CHAT_URL")?.Trim();
			string text3 = Environment.GetEnvironmentVariable("OLLAMA_API_KEY")?.Trim();
			string text4 = Environment.GetEnvironmentVariable("OLLAMA_MODEL")?.Trim();
			if (!string.IsNullOrWhiteSpace(text2))
			{
				requestUri = text2;
			}
			if (text3 != null)
			{
				text = text3;
			}
			if (!string.IsNullOrWhiteSpace(text4))
			{
				model = text4;
			}
		}
		catch
		{
		}
		if (string.IsNullOrWhiteSpace(name))
		{
			return "Введите название модуля";
		}
		string value = ((inputs == null || inputs.Count == 0) ? "(параметров нет)" : string.Join("\n", inputs.Select(((string Name, string Type, string Label) i) => $"- {i.Name} ({i.Type}) — {i.Label}")));
		string value2 = (string.IsNullOrWhiteSpace(scriptFragment) ? "(скрипт ещё не задан)" : scriptFragment);
		string value3 = (string.IsNullOrWhiteSpace(previousDescription) ? "(нет)" : previousDescription);
		string value4 = (string.IsNullOrWhiteSpace(previousScriptFragment) ? "(нет)" : previousScriptFragment);
		string content = "Ты помощник по документации. Пиши кратко и по делу на русском языке. Используй Markdown. Не придумывай функционал, которого нет. Если дан контекст предыдущей версии, добавь раздел 'История изменений' по сравнению с текущей версией.";
		string content2 = $"Сгенерируй краткое описание модуля для каталога. Структура: 1) Краткое резюме (1-2 предложения), 2) Параметры, 3) Пример запуска (если уместно), 4) Предупреждения/требования, 5) История изменений (если есть предыдущая версия).\r\n\r\nНазвание: {name}\r\nВерсия: {version}\r\nПараметры:\r\n{value}\r\n\r\nТекущий скрипт (фрагмент):\r\n{value2}\r\n\r\nПредыдущее описание:\r\n{value3}\r\n\r\nПредыдущий скрипт (фрагмент):\r\n{value4}\r\n\r\nТребования к ответу:\r\n- Формат — Markdown, без лишних преамбул вроде \"Вот описание\".\r\n- Если параметров нет — явно напиши, что модуль не требует ввода.\r\n- Раздел 'История изменений' должен отражать отличия между предыдущей и текущей версиями (кратко: что улучшено, добавлено, удалено).\r\n- Не используй HTML.\r\n";
		ChatRequest value5 = new ChatRequest
		{
			model = model,
			stream = true,
			messages = new List<ChatMessage>
			{
				new ChatMessage
				{
					role = "system",
					content = content
				},
				new ChatMessage
				{
					role = "user",
					content = content2
				}
			}
		};
		using HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Post, requestUri)
		{
			Content = new StringContent(JsonSerializer.Serialize(value5, _json), Encoding.UTF8, "application/json")
		};
		if (!string.IsNullOrEmpty(text))
		{
			msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", text);
		}
		try
		{
			using HttpResponseMessage resp = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(continueOnCapturedContext: false);
			resp.EnsureSuccessStatusCode();
			StringBuilder sb = new StringBuilder();
			try
			{
				await using Stream stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
				using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
				while (!reader.EndOfStream)
				{
					ct.ThrowIfCancellationRequested();
					string text5 = await reader.ReadLineAsync().ConfigureAwait(continueOnCapturedContext: false);
					if (string.IsNullOrWhiteSpace(text5))
					{
						continue;
					}
					try
					{
						StreamChunk streamChunk = JsonSerializer.Deserialize<StreamChunk>(text5, _json);
						if (!string.IsNullOrEmpty(streamChunk?.response))
						{
							sb.Append(streamChunk.response ?? string.Empty);
						}
						else if (!string.IsNullOrEmpty(streamChunk?.message?.content))
						{
							sb.Append(streamChunk?.message?.content ?? string.Empty);
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
			if (sb.Length > 0)
			{
				return sb.ToString();
			}
			string text6 = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
			try
			{
				return (JsonSerializer.Deserialize<ChatResponse>(text6, _json) ?? new ChatResponse()).messages?.LastOrDefault()?.content ?? string.Empty;
			}
			catch
			{
				try
				{
					StreamChunk streamChunk2 = JsonSerializer.Deserialize<StreamChunk>(text6, _json);
					return streamChunk2?.response ?? streamChunk2?.message?.content ?? string.Empty;
				}
				catch
				{
					return text6;
				}
			}
		}
		catch (Exception ex)
		{
			return "Ошибка AI: " + ex.Message;
		}
	}

	public void Dispose()
	{
		_http.Dispose();
	}
}

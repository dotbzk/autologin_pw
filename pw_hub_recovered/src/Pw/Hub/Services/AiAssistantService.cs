using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Pw.Hub.Infrastructure;

namespace Pw.Hub.Services;

public class AiAssistantService : IAiAssistantService, IDisposable
{
	private sealed class AiMessage
	{
		public string role { get; set; } = string.Empty;

		public string content { get; set; } = string.Empty;
	}

	private sealed class AiChatRequest
	{
		public string model { get; set; } = string.Empty;

		public List<AiMessage> messages { get; set; } = new List<AiMessage>();

		public bool stream { get; set; }
	}

	private sealed class AiChatResponse
	{
		public List<AiMessage> messages { get; set; } = new List<AiMessage>();
	}

	private sealed class AiStreamChunk
	{
		public bool done { get; set; }

		public AiMessage message { get; set; }

		public string response { get; set; }
	}

	private readonly IDiffPreviewService _diff;

	private readonly HttpClient _http;

	private readonly List<AiMessage> _messages = new List<AiMessage>();

	private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private string _apiUrl;

	private string _apiKey;

	private string _model;

	private readonly IAiConfigService _cfg;

	public AiAssistantService(IDiffPreviewService diff)
	{
		_diff = diff;
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

	public void NewSession()
	{
		_messages.Clear();
	}

	public async Task<AiAssistantResponse> SendAsync(string prompt, string currentCode, string? manualChangesDiff = null, CancellationToken ct = default(CancellationToken), Action<string>? onStreamDelta = null)
	{
		if (string.IsNullOrWhiteSpace(prompt))
		{
			return new AiAssistantResponse
			{
				AssistantText = "Empty request"
			};
		}
		try
		{
			AiConfig effective = _cfg.GetEffective();
			_apiUrl = effective.ApiUrl;
			_model = effective.Model;
			_apiKey = effective.ApiKey;
		}
		catch
		{
		}
		EnsureSystemPriming();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("=== CURRENT CODE ===");
		stringBuilder.AppendLine("```lua");
		stringBuilder.AppendLine(currentCode ?? string.Empty);
		stringBuilder.AppendLine("```");
		stringBuilder.AppendLine();
		if (!string.IsNullOrWhiteSpace(manualChangesDiff))
		{
			stringBuilder.AppendLine("=== MANUAL EDITS (since the previous AI request) ===");
			stringBuilder.AppendLine(manualChangesDiff);
			stringBuilder.AppendLine();
		}
		stringBuilder.AppendLine("=== REQUEST ===");
		stringBuilder.AppendLine(prompt);
		_messages.Add(new AiMessage
		{
			role = "user",
			content = stringBuilder.ToString()
		});
		AiChatRequest value = new AiChatRequest
		{
			model = _model,
			messages = _messages.ToList(),
			stream = true
		};
		using HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
		{
			Content = new StringContent(JsonSerializer.Serialize(value, _jsonOptions), Encoding.UTF8, "application/json")
		};
		if (!string.IsNullOrEmpty(_apiKey))
		{
			msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
		}
		string text4;
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
					string text = await reader.ReadLineAsync().ConfigureAwait(continueOnCapturedContext: false);
					if (string.IsNullOrWhiteSpace(text))
					{
						continue;
					}
					try
					{
						AiStreamChunk aiStreamChunk = JsonSerializer.Deserialize<AiStreamChunk>(text, _jsonOptions);
						string text2 = null;
						if (!string.IsNullOrEmpty(aiStreamChunk?.message?.content))
						{
							text2 = aiStreamChunk.message.content;
						}
						else if (!string.IsNullOrEmpty(aiStreamChunk?.response))
						{
							text2 = aiStreamChunk.response;
						}
						if (!string.IsNullOrEmpty(text2))
						{
							sb.Append(text2);
							try
							{
								onStreamDelta?.Invoke(text2);
							}
							catch
							{
							}
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
			if (sb.Length == 0)
			{
				string text3 = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
				try
				{
					text4 = (JsonSerializer.Deserialize<AiChatResponse>(text3, _jsonOptions) ?? new AiChatResponse
					{
						messages = new List<AiMessage>()
					}).messages?.LastOrDefault()?.content ?? string.Empty;
				}
				catch
				{
					try
					{
						text4 = JsonSerializer.Deserialize<AiStreamChunk>(text3, _jsonOptions)?.response ?? string.Empty;
					}
					catch
					{
						text4 = text3;
					}
				}
			}
			else
			{
				text4 = sb.ToString();
			}
		}
		catch (Exception ex)
		{
			text4 = "AI request error: " + ex.Message;
		}
		if (string.IsNullOrEmpty(text4))
		{
			text4 = "(empty response)";
		}
		string text5 = ExtractLuaCodeBlock(text4);
		string text6 = ExtractSummaryFromAssistant(text4);
		IList<string> diffLines = Array.Empty<string>();
		if (!string.IsNullOrEmpty(text5))
		{
			try
			{
				diffLines = _diff.BuildUnifiedDiffGit(currentCode?.Replace("\r\n", "\n"), text5.Replace("\r\n", "\n"));
			}
			catch
			{
				diffLines = new List<string> { "Could not build the diff" };
			}
		}
		string summary = (string.IsNullOrWhiteSpace(text6) ? BuildSummary(diffLines) : text6);
		_messages.Add(new AiMessage
		{
			role = "assistant",
			content = text4
		});
		return new AiAssistantResponse
		{
			AssistantText = text4,
			Summary = summary,
			ExtractedCode = (text5 ?? string.Empty),
			DiffLines = diffLines
		};
	}

	private void EnsureSystemPriming()
	{
		if (_messages.Count <= 0 || !(_messages[0].role == "system"))
		{
			string text = string.Empty;
			try
			{
				text = LuaApiRegistry.ToCheatSheetText();
			}
			catch
			{
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("You are an expert in Lua scripts for game-process automation.\nWrite correct, working Lua 5.1 code for the PW Hub environment.");
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("RESPONSE FORMAT:");
			stringBuilder.AppendLine("1) A brief summary of changes (1–2 sentences)");
			stringBuilder.AppendLine("2) A blank line");
			stringBuilder.AppendLine("3) The complete final code in a ```lua ... ``` block");
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("Do not add any sections after the code.");
			stringBuilder.AppendLine();
			if (!string.IsNullOrWhiteSpace(text))
			{
				stringBuilder.AppendLine("AVAILABLE LUA API (v1/v2) — generated from the current code:");
				stringBuilder.AppendLine(text.Trim());
			}
			else
			{
				stringBuilder.AppendLine("AVAILABLE API (all functions are asynchronous and use callbacks):");
				stringBuilder.AppendLine();
				stringBuilder.AppendLine("=== ACCOUNT OPERATIONS ===");
				stringBuilder.AppendLine("Account_GetAccountCb(cb) — get the current account");
				stringBuilder.AppendLine("Account_GetAccountsCb(cb) — get all accounts (table)");
				stringBuilder.AppendLine("Account_IsAuthorizedCb(cb) — check authorization");
				stringBuilder.AppendLine("Account_ChangeAccountCb(accountId, cb) — switch account");
				stringBuilder.AppendLine();
				stringBuilder.AppendLine("=== BROWSER OPERATIONS ===");
				stringBuilder.AppendLine("Browser_NavigateCb(url, cb) — navigate to a URL");
				stringBuilder.AppendLine("Browser_ReloadCb(cb) — reload the page");
				stringBuilder.AppendLine("Browser_ExecuteScriptCb(jsCode, cb) — execute JavaScript");
				stringBuilder.AppendLine("Browser_ElementExistsCb(selector, cb) — check whether an element exists");
				stringBuilder.AppendLine("Browser_WaitForElementCb(selector, timeoutMs, cb) — wait for an element");
				stringBuilder.AppendLine();
				stringBuilder.AppendLine("=== HELPERS ===");
				stringBuilder.AppendLine("Print(text), DelayCb(ms, cb), ReportProgress(p), ReportProgressMsg(p, msg), Complete(result)");
				stringBuilder.AppendLine("Net_PostJsonCb(url, jsonBody, contentType, cb), Telegram_SendMessageCb(text, cb)");
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("=== LAUNCH ARGUMENTS (global args table) ===");
			stringBuilder.AppendLine("When a module defines input parameters, Lua receives their entered values in the global args table.");
			stringBuilder.AppendLine("Example:");
			stringBuilder.AppendLine("```lua");
			stringBuilder.AppendLine("local accounts = (args and args['accounts']) or ''\nPrint('Account count: ' .. tostring(#args['accounts'])");
			stringBuilder.AppendLine("```");
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("IMPORTANT RULES:");
			stringBuilder.AppendLine("1. DECLARE ALL FUNCTIONS FIRST, THEN ASSIGN VALUES BELOW THEM.");
			stringBuilder.AppendLine("2. Use asynchronous functions (…Cb) and callbacks.");
			stringBuilder.AppendLine("3. Define each callback as a separate function.");
			stringBuilder.AppendLine("4. Write comments in English.");
			stringBuilder.AppendLine("5. Handle errors and edge cases.");
			stringBuilder.AppendLine("6. Use clear variable names and log stages with Print().");
			stringBuilder.AppendLine("7. Call Complete at the end when it is available.");
			stringBuilder.AppendLine("8. Wait for an element before accessing it (for example, 1000 ms).");
			stringBuilder.AppendLine("9. Navigation is allowed only on the pwonline.ru domain.");
			stringBuilder.AppendLine("10. JavaScript must return a string; encode arrays as strings and parse them in Lua.");
			_messages.Insert(0, new AiMessage
			{
				role = "system",
				content = stringBuilder.ToString()
			});
		}
	}

	private static string ExtractLuaCodeBlock(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		Match match = Regex.Match(text, "```lua\\s*(.*?)```", RegexOptions.IgnoreCase | RegexOptions.Singleline);
		if (match.Success)
		{
			return match.Groups[1].Value.Trim();
		}
		match = Regex.Match(text, "```\\s*(.*?)```", RegexOptions.Singleline);
		if (match.Success)
		{
			return match.Groups[1].Value.Trim();
		}
		return string.Empty;
	}

	private static string ExtractSummaryFromAssistant(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		try
		{
			int num = text.IndexOf("```", StringComparison.Ordinal);
			string text2 = ((num >= 0) ? text.Substring(0, num) : text);
			text2 = text2.Replace("\r\n", "\n").Trim();
			if (string.IsNullOrWhiteSpace(text2))
			{
				return string.Empty;
			}
			int num2 = text2.IndexOf("\n\n", StringComparison.Ordinal);
			string text3 = Regex.Replace(Regex.Replace((num2 >= 0) ? text2.Substring(0, num2) : text2, "^[#>*\\s-]+", string.Empty, RegexOptions.Multiline).Trim(), "\n+", " ").Trim();
			if (text3.Length > 300)
			{
				text3 = text3.Substring(0, 297) + "...";
			}
			return text3;
		}
		catch
		{
			return string.Empty;
		}
	}

	public void Dispose()
	{
		_http.Dispose();
	}

	private static string BuildSummary(IList<string> diffLines)
	{
		if (diffLines == null || diffLines.Count == 0)
		{
			return "No changes. The code is identical to the current version.";
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		foreach (string diffLine in diffLines)
		{
			if (!string.IsNullOrEmpty(diffLine))
			{
				if (diffLine.StartsWith("@@"))
				{
					num3++;
				}
				else if (diffLine.StartsWith("+") && !diffLine.StartsWith("+++"))
				{
					num++;
				}
				else if (diffLine.StartsWith("-") && !diffLine.StartsWith("---"))
				{
					num2++;
				}
			}
		}
		if (num != 0 || num2 != 0)
		{
			return $"Changes: +{num} / -{num2} lines in {num3} block(s). Select 'Show Full Changes' to review the diff.";
		}
		return "No changes. The code is identical to the current version.";
	}
}

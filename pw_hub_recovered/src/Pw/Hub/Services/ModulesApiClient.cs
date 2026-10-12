using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;

namespace Pw.Hub.Services;

public class ModulesApiClient
{
	public sealed class TelegramLinkInfo
	{
		public string Link { get; set; }

		public string State { get; set; }

		public string BotUsername { get; set; }

		public DateTimeOffset ExpiresAt { get; set; }
	}

	private readonly HttpClient _http;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public string BaseUrl { get; }

	public string Token { get; private set; }

	public UserDto CurrentUser { get; private set; }

	public ModulesApiClient(string baseUrl = null, HttpMessageHandler handler = null)
	{
		BaseUrl = baseUrl?.TrimEnd('/') ?? Environment.GetEnvironmentVariable("PW_MODULES_API")?.TrimEnd('/') ?? "https://api.pw-hub.ru";
		_http = ((handler == null) ? new HttpClient() : new HttpClient(handler));
		_http.Timeout = TimeSpan.FromSeconds(15.0);
		try
		{
			AuthState.Load();
			if (!string.IsNullOrWhiteSpace(AuthState.Token))
			{
				Token = AuthState.Token;
				ApplyAuthHeader();
			}
		}
		catch
		{
		}
	}

	private void ApplyAuthHeader()
	{
		_http.DefaultRequestHeaders.Remove("X-Auth-Token");
		if (!string.IsNullOrEmpty(Token))
		{
			_http.DefaultRequestHeaders.Add("X-Auth-Token", Token);
		}
	}

	public async Task<AuthResponse> RegisterAsync(string username, string password)
	{
		string requestUri = BaseUrl + "/api/auth/register";
		HttpResponseMessage httpResponseMessage = await _http.PostAsync(requestUri, new StringContent(JsonSerializer.Serialize(new { username, password }, JsonOptions), Encoding.UTF8, "application/json"));
		if (!httpResponseMessage.IsSuccessStatusCode)
		{
			return null;
		}
		AuthResponse authResponse = JsonSerializer.Deserialize<AuthResponse>(await httpResponseMessage.Content.ReadAsStringAsync(), JsonOptions);
		if (authResponse != null)
		{
			Token = authResponse.Token;
			ApplyAuthHeader();
			CurrentUser = new UserDto
			{
				UserId = authResponse.UserId,
				Username = authResponse.Username,
				Developer = authResponse.Developer
			};
		}
		return authResponse;
	}

	public async Task<AuthResponse> LoginAsync(string username, string password)
	{
		string requestUri = BaseUrl + "/api/auth/login";
		HttpResponseMessage httpResponseMessage = await _http.PostAsync(requestUri, new StringContent(JsonSerializer.Serialize(new { username, password }, JsonOptions), Encoding.UTF8, "application/json"));
		if (!httpResponseMessage.IsSuccessStatusCode)
		{
			return null;
		}
		AuthResponse authResponse = JsonSerializer.Deserialize<AuthResponse>(await httpResponseMessage.Content.ReadAsStringAsync(), JsonOptions);
		if (authResponse != null)
		{
			Token = authResponse.Token;
			ApplyAuthHeader();
			CurrentUser = new UserDto
			{
				UserId = authResponse.UserId,
				Username = authResponse.Username,
				Developer = authResponse.Developer
			};
		}
		return authResponse;
	}

	public async Task<UserDto> MeAsync()
	{
		ApplyAuthHeader();
		HttpResponseMessage httpResponseMessage = await _http.GetAsync(BaseUrl + "/api/auth/me");
		if (!httpResponseMessage.IsSuccessStatusCode)
		{
			return null;
		}
		UserDto userDto = JsonSerializer.Deserialize<UserDto>(await httpResponseMessage.Content.ReadAsStringAsync(), JsonOptions);
		if (userDto != null)
		{
			CurrentUser = userDto;
		}
		return userDto;
	}

	public async Task<UserDto> UpdateUsernameAsync(string username)
	{
		ApplyAuthHeader();
		string requestUri = BaseUrl + "/api/auth/username";
		var value = new { username };
		HttpResponseMessage httpResponseMessage = await _http.PostAsync(requestUri, new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json"));
		if (!httpResponseMessage.IsSuccessStatusCode)
		{
			return null;
		}
		UserDto userDto = JsonSerializer.Deserialize<UserDto>(await httpResponseMessage.Content.ReadAsStringAsync(), JsonOptions);
		if (userDto != null)
		{
			CurrentUser = userDto;
		}
		return userDto;
	}

	public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
	{
		ApplyAuthHeader();
		string requestUri = BaseUrl + "/api/auth/password";
		var value = new { currentPassword, newPassword };
		return (await _http.PostAsync(requestUri, new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json"))).IsSuccessStatusCode;
	}

	public async Task<TelegramLinkInfo> GenerateTelegramLinkAsync()
	{
		ApplyAuthHeader();
		string requestUri = BaseUrl + "/api/auth/telegram/link";
		HttpResponseMessage httpResponseMessage = await _http.PostAsync(requestUri, new StringContent(string.Empty, Encoding.UTF8, "application/json"));
		if (!httpResponseMessage.IsSuccessStatusCode)
		{
			return null;
		}
		return JsonSerializer.Deserialize<TelegramLinkInfo>(await httpResponseMessage.Content.ReadAsStringAsync(), JsonOptions);
	}

	public async Task<UserDto> UnlinkTelegramAsync()
	{
		ApplyAuthHeader();
		string requestUri = BaseUrl + "/api/auth/telegram/unlink";
		HttpResponseMessage httpResponseMessage = await _http.PostAsync(requestUri, new StringContent(string.Empty, Encoding.UTF8, "application/json"));
		if (!httpResponseMessage.IsSuccessStatusCode)
		{
			return null;
		}
		UserDto userDto = JsonSerializer.Deserialize<UserDto>(await httpResponseMessage.Content.ReadAsStringAsync(), JsonOptions);
		if (userDto != null)
		{
			CurrentUser = userDto;
		}
		return userDto;
	}

	public async Task<bool> SendTelegramMessageAsync(string message)
	{
		ApplyAuthHeader();
		string requestUri = BaseUrl + "/api/auth/telegram/send";
		StringContent content = new StringContent(JsonSerializer.Serialize(new { message }, JsonOptions), Encoding.UTF8, "application/json");
		using HttpResponseMessage httpResponseMessage = await _http.PostAsync(requestUri, content);
		return httpResponseMessage.IsSuccessStatusCode;
	}

	public async Task<PagedModulesResponse> SearchAsync(string q = null, string tags = null, string sort = null, string order = null, int page = 1, int pageSize = 20)
	{
		UriBuilder uriBuilder = new UriBuilder(BaseUrl + "/api/modules");
		NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(uriBuilder.Query);
		if (!string.IsNullOrWhiteSpace(q))
		{
			nameValueCollection["q"] = q;
		}
		if (!string.IsNullOrWhiteSpace(tags))
		{
			nameValueCollection["tags"] = tags;
		}
		if (!string.IsNullOrWhiteSpace(sort))
		{
			nameValueCollection["sort"] = sort;
		}
		if (!string.IsNullOrWhiteSpace(order))
		{
			nameValueCollection["order"] = order;
		}
		if (page > 1)
		{
			nameValueCollection["page"] = page.ToString();
		}
		if (pageSize != 20)
		{
			nameValueCollection["pageSize"] = pageSize.ToString();
		}
		uriBuilder.Query = nameValueCollection.ToString();
		string requestUri = uriBuilder.ToString();
		ApplyAuthHeader();
		using HttpResponseMessage resp = await _http.GetAsync(requestUri);
		resp.EnsureSuccessStatusCode();
		return JsonSerializer.Deserialize<PagedModulesResponse>(await resp.Content.ReadAsStringAsync(), JsonOptions) ?? new PagedModulesResponse();
	}

	public async Task<ModuleDto> CreateModuleAsync(CreateOrUpdateModule req)
	{
		ApplyAuthHeader();
		string requestUri = BaseUrl + "/api/modules";
		HttpResponseMessage httpResponseMessage = await _http.PostAsync(requestUri, new StringContent(JsonSerializer.Serialize(req, JsonOptions), Encoding.UTF8, "application/json"));
		if (!httpResponseMessage.IsSuccessStatusCode)
		{
			return null;
		}
		return JsonSerializer.Deserialize<ModuleDto>(await httpResponseMessage.Content.ReadAsStringAsync(), JsonOptions);
	}

	public async Task<ModuleDto> UpdateModuleAsync(Guid id, CreateOrUpdateModule req)
	{
		ApplyAuthHeader();
		string requestUri = $"{BaseUrl}/api/modules/{id}";
		HttpResponseMessage httpResponseMessage = await _http.PutAsync(requestUri, new StringContent(JsonSerializer.Serialize(req, JsonOptions), Encoding.UTF8, "application/json"));
		if (!httpResponseMessage.IsSuccessStatusCode)
		{
			return null;
		}
		return JsonSerializer.Deserialize<ModuleDto>(await httpResponseMessage.Content.ReadAsStringAsync(), JsonOptions);
	}

	public async Task<bool> DeleteModuleAsync(Guid id)
	{
		ApplyAuthHeader();
		string requestUri = $"{BaseUrl}/api/modules/{id}";
		return (await _http.DeleteAsync(requestUri)).IsSuccessStatusCode;
	}

	public async Task<ModuleDto> InstallAsync(Guid id, string userId)
	{
		ApplyAuthHeader();
		string requestUri = $"{BaseUrl}/api/modules/{id}/install?userId={Uri.EscapeDataString(userId ?? string.Empty)}";
		using HttpResponseMessage resp = await _http.PostAsync(requestUri, new StringContent(string.Empty, Encoding.UTF8, "application/json"));
		if (resp.IsSuccessStatusCode)
		{
			return JsonSerializer.Deserialize<ModuleDto>(await resp.Content.ReadAsStringAsync(), JsonOptions);
		}
		return null;
	}

	public async Task<ModuleDto> UninstallAsync(Guid id, string userId)
	{
		ApplyAuthHeader();
		string requestUri = $"{BaseUrl}/api/modules/{id}/install?userId={Uri.EscapeDataString(userId ?? string.Empty)}";
		using HttpResponseMessage resp = await _http.DeleteAsync(requestUri);
		if (resp.IsSuccessStatusCode)
		{
			return JsonSerializer.Deserialize<ModuleDto>(await resp.Content.ReadAsStringAsync(), JsonOptions);
		}
		return null;
	}

	public async Task<ModuleDto> IncrementRunAsync(Guid id)
	{
		string requestUri = $"{BaseUrl}/api/modules/{id}/run";
		using HttpResponseMessage resp = await _http.PostAsync(requestUri, new StringContent(string.Empty, Encoding.UTF8, "application/json"));
		if (resp.IsSuccessStatusCode)
		{
			return JsonSerializer.Deserialize<ModuleDto>(await resp.Content.ReadAsStringAsync(), JsonOptions);
		}
		return null;
	}

	public async Task<List<ModuleDto>> GetInstalledAsync(string userId)
	{
		string requestUri = BaseUrl + "/api/modules/installed?userId=" + Uri.EscapeDataString(userId ?? string.Empty);
		ApplyAuthHeader();
		using HttpResponseMessage resp = await _http.GetAsync(requestUri);
		if (!resp.IsSuccessStatusCode)
		{
			return new List<ModuleDto>();
		}
		return JsonSerializer.Deserialize<List<ModuleDto>>(await resp.Content.ReadAsStringAsync(), JsonOptions) ?? new List<ModuleDto>();
	}

	public async Task<ModuleDto> GetModuleAsync(Guid id)
	{
		ApplyAuthHeader();
		string requestUri = $"{BaseUrl}/api/modules/{id}";
		using HttpResponseMessage resp = await _http.GetAsync(requestUri);
		if (!resp.IsSuccessStatusCode)
		{
			return null;
		}
		return JsonSerializer.Deserialize<ModuleDto>(await resp.Content.ReadAsStringAsync(), JsonOptions);
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Pw.Hub.Infrastructure.Logging;
using Pw.Hub.Models;
using Cookie = Pw.Hub.Models.Cookie;

namespace Pw.Hub.Services;

public sealed class BrowserSessionBridge : IDisposable
{
	public const int Port = 43827;

	private const int MaxHeaderBytes = 32768;

	private const int MaxBodyBytes = 1048576;

	private const string ExtensionMarker = "pw-hub-session-import-v1";

	private static readonly ILogger _log = Log.For<BrowserSessionBridge>();

	private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private readonly Func<Account?> _getSelectedAccount;

	private readonly Func<Cookie[], Task> _importSessionAsync;

	private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

	private readonly string _accessToken;

	private TcpListener? _listener;

	private string? _pairingCode;

	private DateTime _pairingExpiresUtc;

	private int _pairingAttemptsRemaining;

	public BrowserSessionBridge(Func<Account?> getSelectedAccount, Func<Cookie[], Task> importSessionAsync)
	{
		_getSelectedAccount = getSelectedAccount ?? throw new ArgumentNullException(nameof(getSelectedAccount));
		_importSessionAsync = importSessionAsync ?? throw new ArgumentNullException(nameof(importSessionAsync));
		_accessToken = LoadOrCreateAccessToken();
	}

	public void Start()
	{
		if (_listener != null)
		{
			return;
		}
		_listener = new TcpListener(IPAddress.Loopback, Port);
		_listener.Start();
		_ = AcceptLoopAsync(_cancellation.Token);
		_log.Info($"Browser session bridge listening on 127.0.0.1:{Port}");
	}

	public PairingInfo BeginPairing()
	{
		Account account = _getSelectedAccount()
			?? throw new InvalidOperationException("Перед подключением расширения браузера выберите аккаунт.");
		_pairingCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
		_pairingExpiresUtc = DateTime.UtcNow.AddMinutes(5);
		_pairingAttemptsRemaining = 8;
		return new PairingInfo(_pairingCode, account.Name, _pairingExpiresUtc);
	}

	private async Task AcceptLoopAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				TcpClient client = await _listener!.AcceptTcpClientAsync(cancellationToken);
				_ = HandleClientAsync(client, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (ObjectDisposedException)
			{
				break;
			}
			catch (Exception ex)
			{
				_log.Warn("Browser session bridge accept failed", new Dictionary<string, object> { { "error", ex.Message } });
				await Task.Delay(250, cancellationToken);
			}
		}
	}

	private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
	{
		using (client)
		{
			string? origin = null;
			try
			{
				client.NoDelay = true;
				HttpRequest request = await ReadRequestAsync(client.GetStream(), cancellationToken);
				origin = request.Headers.TryGetValue("Origin", out string? originValue) ? originValue : null;
				if (request.Method == "OPTIONS")
				{
					await WriteResponseAsync(client.GetStream(), 204, null, origin, cancellationToken);
					return;
				}
				if (!IsExtensionRequest(request, origin))
				{
					_log.Warn("Rejected browser session bridge request", new Dictionary<string, object>
					{
						{ "origin", origin ?? "<none>" },
						{ "path", request.Path }
					});
					await WriteResponseAsync(client.GetStream(), 403, new { error = "Запросы принимаются только от расширения браузера PW Hub." }, null, cancellationToken);
					return;
				}
				switch ((request.Method, request.Path))
				{
					case ("POST", "/pwhub/pair"):
						await HandlePairAsync(client.GetStream(), request, origin, cancellationToken);
						break;
					case ("GET", "/pwhub/status"):
						await HandleStatusAsync(client.GetStream(), request, origin, cancellationToken);
						break;
					case ("POST", "/pwhub/import"):
						await HandleImportAsync(client.GetStream(), request, origin, cancellationToken);
						break;
					default:
						await WriteResponseAsync(client.GetStream(), 404, new { error = "Endpoint not found." }, origin, cancellationToken);
						break;
				}
			}
			catch (InvalidDataException ex)
			{
				await TryWriteErrorAsync(client, 400, ex.Message, origin, cancellationToken);
			}
			catch (Exception ex)
			{
				_log.Warn("Browser session bridge request failed", new Dictionary<string, object> { { "error", ex.Message } });
				await TryWriteErrorAsync(client, 500, "PW Hub не смог обработать браузерную сессию.", origin, cancellationToken);
			}
		}
	}

	private async Task HandlePairAsync(NetworkStream stream, HttpRequest request, string? origin, CancellationToken cancellationToken)
	{
		PairRequest? payload = JsonSerializer.Deserialize<PairRequest>(request.Body, _jsonOptions);
		if (_pairingCode == null || DateTime.UtcNow > _pairingExpiresUtc || _pairingAttemptsRemaining <= 0)
		{
			await WriteResponseAsync(stream, 401, new { error = "Срок действия кода подключения истёк. Запросите новый код в PW Hub." }, origin, cancellationToken);
			return;
		}
		_pairingAttemptsRemaining--;
		if (payload == null || !FixedTimeEquals(payload.Code, _pairingCode))
		{
			await WriteResponseAsync(stream, 401, new { error = "Неверный код подключения." }, origin, cancellationToken);
			return;
		}
		_pairingCode = null;
		Account? account = _getSelectedAccount();
		await WriteResponseAsync(stream, 200, new
		{
			token = _accessToken,
			accountId = account?.Id,
			accountName = account?.Name
		}, origin, cancellationToken);
	}

	private async Task HandleStatusAsync(NetworkStream stream, HttpRequest request, string? origin, CancellationToken cancellationToken)
	{
		if (!IsAuthorized(request))
		{
			await WriteResponseAsync(stream, 401, new { error = "Расширение не подключено к PW Hub." }, origin, cancellationToken);
			return;
		}
		Account? account = _getSelectedAccount();
		await WriteResponseAsync(stream, 200, new
		{
			ready = account != null,
			accountId = account?.Id,
			accountName = account?.Name,
			message = account == null ? "Выберите аккаунт в PW Hub." : "Готово к импорту текущей сессии PW."
		}, origin, cancellationToken);
	}

	private async Task HandleImportAsync(NetworkStream stream, HttpRequest request, string? origin, CancellationToken cancellationToken)
	{
		if (!IsAuthorized(request))
		{
			await WriteResponseAsync(stream, 401, new { error = "Расширение не подключено к PW Hub." }, origin, cancellationToken);
			return;
		}
		Account account = _getSelectedAccount()
			?? throw new InvalidOperationException("Перед импортом браузерной сессии выберите аккаунт в PW Hub.");
		ImportRequest? payload = JsonSerializer.Deserialize<ImportRequest>(request.Body, _jsonOptions);
		if (payload == null || !string.Equals(payload.AccountId, account.Id, StringComparison.Ordinal))
		{
			await WriteResponseAsync(stream, 400, new { error = "Выбранный аккаунт PW Hub изменился. Обновите расширение и повторите попытку." }, origin, cancellationToken);
			return;
		}
		Cookie[] cookies = (payload?.Cookies ?? Array.Empty<Cookie>())
			.Where(IsSafeCookie)
			.Take(500)
			.ToArray();
		if (cookies.Length == 0)
		{
			await WriteResponseAsync(stream, 400, new { error = "В браузере не найдены поддерживаемые cookies PW." }, origin, cancellationToken);
			return;
		}
		try
		{
			await _importSessionAsync(cookies);
		}
		catch (InvalidOperationException ex)
		{
			await WriteResponseAsync(stream, 400, new { error = ex.Message }, origin, cancellationToken);
			return;
		}
		await WriteResponseAsync(stream, 200, new
		{
			ok = true,
			accountId = account.Id,
			accountName = account.Name,
			cookiesImported = cookies.Length
		}, origin, cancellationToken);
	}

	private bool IsAuthorized(HttpRequest request)
	{
		return request.Headers.TryGetValue("X-PW-Hub-Token", out string? token)
			&& FixedTimeEquals(token, _accessToken);
	}

	private static bool IsSafeCookie(Cookie cookie)
	{
		if (cookie == null || string.IsNullOrWhiteSpace(cookie.Name) || cookie.Name.Length > 256
			|| cookie.Value == null || cookie.Value.Length > 16384 || string.IsNullOrWhiteSpace(cookie.Domain)
			|| cookie.Domain.Length > 253 || string.IsNullOrWhiteSpace(cookie.Path) || cookie.Path.Length > 2048)
		{
			return false;
		}
		string domain = cookie.Domain.Trim().TrimStart('.').ToLowerInvariant();
		return domain == "pwonline.ru" || domain.EndsWith(".pwonline.ru", StringComparison.Ordinal)
			|| domain == "pw.mail.ru" || domain.EndsWith(".pw.mail.ru", StringComparison.Ordinal);
	}

	private static bool IsExtensionOrigin(string? origin)
	{
		return origin != null && (origin.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase)
			|| origin.StartsWith("extension://", StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsExtensionRequest(HttpRequest request, string? origin)
	{
		return IsExtensionOrigin(origin)
			|| (request.Headers.TryGetValue("X-PW-Hub-Extension", out string? marker)
				&& FixedTimeEquals(marker, ExtensionMarker));
	}

	private static bool FixedTimeEquals(string? left, string? right)
	{
		if (left == null || right == null)
		{
			return false;
		}
		byte[] leftBytes = Encoding.UTF8.GetBytes(left);
		byte[] rightBytes = Encoding.UTF8.GetBytes(right);
		return leftBytes.Length == rightBytes.Length
			&& CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
	}

	private static async Task<HttpRequest> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
	{
		using MemoryStream received = new MemoryStream();
		byte[] buffer = new byte[4096];
		int headerEnd = -1;
		while (headerEnd < 0)
		{
			int read = await stream.ReadAsync(buffer, cancellationToken);
			if (read == 0)
			{
				throw new InvalidDataException("The request ended before its headers were received.");
			}
			received.Write(buffer, 0, read);
			if (received.Length > MaxHeaderBytes)
			{
				throw new InvalidDataException("The request headers are too large.");
			}
			headerEnd = FindHeaderEnd(received.GetBuffer(), (int)received.Length);
		}
		byte[] allBytes = received.ToArray();
		string headerText = Encoding.ASCII.GetString(allBytes, 0, headerEnd);
		string[] lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
		string[] requestLine = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (requestLine.Length < 2)
		{
			throw new InvalidDataException("The HTTP request line is invalid.");
		}
		Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (string line in lines.Skip(1))
		{
			int separator = line.IndexOf(':');
			if (separator > 0)
			{
				headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
			}
		}
		int bodyOffset = headerEnd + 4;
		byte[] initialBody = allBytes[bodyOffset..];
		string bodyText;
		if (headers.TryGetValue("Transfer-Encoding", out string? transferEncoding)
			&& transferEncoding.Split(',').Any(value => value.Trim().Equals("chunked", StringComparison.OrdinalIgnoreCase)))
		{
			bodyText = Encoding.UTF8.GetString(await ReadChunkedBodyAsync(stream, initialBody, cancellationToken));
		}
		else
		{
			int contentLength = 0;
			if (headers.TryGetValue("Content-Length", out string? lengthText)
				&& (!int.TryParse(lengthText, out contentLength) || contentLength < 0 || contentLength > MaxBodyBytes))
			{
				throw new InvalidDataException("The request body is too large.");
			}
			if (initialBody.Length > contentLength)
			{
				initialBody = initialBody[..contentLength];
			}
			using MemoryStream body = new MemoryStream(contentLength);
			body.Write(initialBody, 0, initialBody.Length);
			while (body.Length < contentLength)
			{
				int remaining = contentLength - (int)body.Length;
				int read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken);
				if (read == 0)
				{
					throw new InvalidDataException("The request body ended unexpectedly.");
				}
				body.Write(buffer, 0, read);
			}
			bodyText = Encoding.UTF8.GetString(body.ToArray());
		}
		string path = requestLine[1].Split('?', 2)[0];
		return new HttpRequest(requestLine[0].ToUpperInvariant(), path, headers, bodyText);
	}

	private static async Task<byte[]> ReadChunkedBodyAsync(NetworkStream stream, byte[] initialBytes, CancellationToken cancellationToken)
	{
		BufferedRequestReader reader = new BufferedRequestReader(stream, initialBytes);
		using MemoryStream body = new MemoryStream();
		while (true)
		{
			string sizeLine = await reader.ReadLineAsync(cancellationToken);
			string sizeText = sizeLine.Split(';', 2)[0].Trim();
			if (!int.TryParse(sizeText, System.Globalization.NumberStyles.HexNumber,
				System.Globalization.CultureInfo.InvariantCulture, out int chunkSize) || chunkSize < 0)
			{
				throw new InvalidDataException("The chunked request body is invalid.");
			}
			if (chunkSize == 0)
			{
				while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
				{
				}
				break;
			}
			if (body.Length + chunkSize > MaxBodyBytes)
			{
				throw new InvalidDataException("The request body is too large.");
			}
			await reader.CopyExactlyAsync(body, chunkSize, cancellationToken);
			if (await reader.ReadByteAsync(cancellationToken) != '\r'
				|| await reader.ReadByteAsync(cancellationToken) != '\n')
			{
				throw new InvalidDataException("The chunked request body is invalid.");
			}
		}
		return body.ToArray();
	}

	private sealed class BufferedRequestReader
	{
		private readonly NetworkStream _stream;
		private readonly byte[] _initialBytes;
		private readonly byte[] _networkBuffer = new byte[4096];
		private int _initialOffset;
		private int _networkOffset;
		private int _networkCount;

		public BufferedRequestReader(NetworkStream stream, byte[] initialBytes)
		{
			_stream = stream;
			_initialBytes = initialBytes;
		}

		public async Task<int> ReadByteAsync(CancellationToken cancellationToken)
		{
			if (_initialOffset < _initialBytes.Length)
			{
				return _initialBytes[_initialOffset++];
			}
			if (_networkOffset >= _networkCount)
			{
				_networkCount = await _stream.ReadAsync(_networkBuffer, cancellationToken);
				_networkOffset = 0;
				if (_networkCount == 0)
				{
					throw new InvalidDataException("The request body ended unexpectedly.");
				}
			}
			return _networkBuffer[_networkOffset++];
		}

		public async Task<string> ReadLineAsync(CancellationToken cancellationToken)
		{
			using MemoryStream line = new MemoryStream();
			while (line.Length <= 8192)
			{
				int value = await ReadByteAsync(cancellationToken);
				if (value == '\r')
				{
					if (await ReadByteAsync(cancellationToken) != '\n')
					{
						throw new InvalidDataException("The chunked request body is invalid.");
					}
					return Encoding.ASCII.GetString(line.ToArray());
				}
				line.WriteByte((byte)value);
			}
			throw new InvalidDataException("A chunked request line is too large.");
		}

		public async Task CopyExactlyAsync(Stream destination, int count, CancellationToken cancellationToken)
		{
			for (int index = 0; index < count; index++)
			{
				destination.WriteByte((byte)await ReadByteAsync(cancellationToken));
			}
		}
	}

	private static int FindHeaderEnd(byte[] bytes, int count)
	{
		for (int i = 0; i <= count - 4; i++)
		{
			if (bytes[i] == 13 && bytes[i + 1] == 10 && bytes[i + 2] == 13 && bytes[i + 3] == 10)
			{
				return i;
			}
		}
		return -1;
	}

	private static async Task WriteResponseAsync(NetworkStream stream, int statusCode, object? payload, string? origin, CancellationToken cancellationToken)
	{
		byte[] body = payload == null ? Array.Empty<byte>() : JsonSerializer.SerializeToUtf8Bytes(payload, _jsonOptions);
		string statusText = statusCode switch
		{
			200 => "OK",
			204 => "No Content",
			400 => "Bad Request",
			401 => "Unauthorized",
			403 => "Forbidden",
			404 => "Not Found",
			_ => "Internal Server Error"
		};
		StringBuilder headers = new StringBuilder();
		headers.Append($"HTTP/1.1 {statusCode} {statusText}\r\n");
		headers.Append("Content-Type: application/json; charset=utf-8\r\n");
		headers.Append($"Content-Length: {body.Length}\r\n");
		headers.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
		headers.Append("Access-Control-Allow-Headers: Content-Type, X-PW-Hub-Token, X-PW-Hub-Extension\r\n");
		headers.Append("Access-Control-Allow-Private-Network: true\r\n");
		headers.Append("Cache-Control: no-store\r\n");
		if (IsExtensionOrigin(origin))
		{
			headers.Append($"Access-Control-Allow-Origin: {origin}\r\n");
			headers.Append("Vary: Origin\r\n");
		}
		headers.Append("Connection: close\r\n\r\n");
		await stream.WriteAsync(Encoding.ASCII.GetBytes(headers.ToString()), cancellationToken);
		if (body.Length > 0)
		{
			await stream.WriteAsync(body, cancellationToken);
		}
	}

	private static async Task TryWriteErrorAsync(TcpClient client, int statusCode, string message, string? origin, CancellationToken cancellationToken)
	{
		try
		{
			if (client.Connected)
			{
				await WriteResponseAsync(client.GetStream(), statusCode, new { error = message }, origin, cancellationToken);
			}
		}
		catch
		{
		}
	}

	private static string LoadOrCreateAccessToken()
	{
		string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameToolsHub", "PwHub");
		string path = Path.Combine(directory, "browser-extension.json");
		try
		{
			if (File.Exists(path))
			{
				BridgeSettings? settings = JsonSerializer.Deserialize<BridgeSettings>(File.ReadAllText(path), _jsonOptions);
				if (!string.IsNullOrWhiteSpace(settings?.Token) && settings.Token.Length >= 32)
				{
					return settings.Token;
				}
			}
			Directory.CreateDirectory(directory);
			string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
			string temporary = path + ".tmp";
			File.WriteAllText(temporary, JsonSerializer.Serialize(new BridgeSettings { Token = token }, _jsonOptions));
			File.Move(temporary, path, overwrite: true);
			return token;
		}
		catch
		{
			return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
		}
	}

	public void Dispose()
	{
		_cancellation.Cancel();
		try
		{
			_listener?.Stop();
		}
		catch
		{
		}
		_cancellation.Dispose();
	}

	public sealed record PairingInfo(string Code, string AccountName, DateTime ExpiresUtc);

	private sealed record HttpRequest(string Method, string Path, Dictionary<string, string> Headers, string Body);

	private sealed class PairRequest
	{
		public string? Code { get; set; }
	}

	private sealed class ImportRequest
	{
		public string? AccountId { get; set; }

		public Cookie[]? Cookies { get; set; }
	}

	private sealed class BridgeSettings
	{
		public string? Token { get; set; }
	}
}

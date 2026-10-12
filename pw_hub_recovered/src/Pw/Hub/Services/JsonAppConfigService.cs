using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Pw.Hub.Services;

public class JsonAppConfigService : IAppConfigService
{
	private readonly string _filePath;

	private readonly object _lock = new object();

	private Dictionary<string, string> _store = new Dictionary<string, string>();

	private int _dirty;

	public JsonAppConfigService()
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PwHub");
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
			_filePath = Path.Combine(text, "config.json");
			Load();
		}
		catch
		{
			_filePath = Path.GetFullPath("config.json");
			_store = new Dictionary<string, string>();
		}
	}

	private void Load()
	{
		try
		{
			if (!File.Exists(_filePath))
			{
				_store = new Dictionary<string, string>();
				return;
			}
			string text = File.ReadAllText(_filePath);
			using JsonDocument jsonDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
			if (jsonDocument.RootElement.ValueKind == JsonValueKind.Object)
			{
				Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				foreach (JsonProperty item in jsonDocument.RootElement.EnumerateObject())
				{
					dictionary[item.Name] = item.Value.GetRawText();
				}
				_store = dictionary;
			}
			else
			{
				_store = new Dictionary<string, string>();
			}
		}
		catch
		{
			try
			{
				string destFileName = _filePath + ".bak";
				File.Copy(_filePath, destFileName, overwrite: true);
			}
			catch
			{
			}
			_store = new Dictionary<string, string>();
		}
	}

	public bool TryGetString(string key, out string? value)
	{
		lock (_lock)
		{
			if (_store.TryGetValue(key, out var value2))
			{
				value = value2;
				return true;
			}
		}
		value = null;
		return false;
	}

	public void SetString(string key, string? value)
	{
		lock (_lock)
		{
			if (value == null)
			{
				_store[key] = "null";
			}
			else
			{
				_store[key] = value;
			}
			Interlocked.Exchange(ref _dirty, 1);
		}
	}

	public async Task SaveAsync()
	{
		if (Interlocked.Exchange(ref _dirty, 0) == 0)
		{
			return;
		}
		Dictionary<string, string> snapshot;
		lock (_lock)
		{
			snapshot = new Dictionary<string, string>(_store);
		}
		try
		{
			using StreamWriter sw = new StreamWriter(_filePath, append: false);
			await sw.WriteAsync("{");
			bool first = true;
			foreach (KeyValuePair<string, string> kv in snapshot)
			{
				if (!first)
				{
					await sw.WriteAsync(",");
				}
				first = false;
				await sw.WriteAsync(JsonSerializer.Serialize(kv.Key));
				await sw.WriteAsync(":");
				await sw.WriteAsync(string.IsNullOrWhiteSpace(kv.Value) ? "null" : kv.Value);
			}
			await sw.WriteAsync("}");
		}
		catch
		{
		}
	}
}

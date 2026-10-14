using System;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Pw.Hub.Infrastructure;

namespace Pw.Hub.ViewModels;

public class ApiKeySettingsViewModel : BaseViewModel
{
	private string _apiKey = string.Empty;

	public string ApiKey
	{
		get
		{
			return _apiKey;
		}
		set
		{
			_apiKey = value ?? string.Empty;
			OnPropertyChanged("ApiKey");
		}
	}

	public ICommand SaveCommand { get; }

	public ICommand CancelCommand { get; }

	public event Action<bool?> RequestClose;

	public ApiKeySettingsViewModel()
	{
		SaveCommand = new RelayCommand(delegate
		{
			Save();
		}, (object _) => CanSave());
		CancelCommand = new RelayCommand(delegate
		{
			this.RequestClose?.Invoke(false);
		});
	}

	private bool CanSave()
	{
		return true;
	}

	private void Save()
	{
		try
		{
			string text = Path.Combine(AppContext.BaseDirectory, "config");
			Directory.CreateDirectory(text);
			string path = Path.Combine(text, "ai_settings.json");
			var value = new
			{
				OllamaApiKey = ApiKey?.Trim()
			};
			File.WriteAllText(path, JsonSerializer.Serialize(value));
			this.RequestClose?.Invoke(true);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException("Could not save settings: " + ex.Message, ex);
		}
	}
}

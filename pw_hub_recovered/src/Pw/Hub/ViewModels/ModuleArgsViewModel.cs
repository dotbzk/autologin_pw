using System;
using System.Collections.Generic;
using System.Windows.Input;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;

namespace Pw.Hub.ViewModels;

public class ModuleArgsViewModel : BaseViewModel
{
	private string _title = "Параметры модуля";

	private ModuleDefinition _module;

	public string Title
	{
		get
		{
			return _title;
		}
		set
		{
			_title = value ?? string.Empty;
			OnPropertyChanged("Title");
		}
	}

	public ModuleDefinition Module
	{
		get
		{
			return _module;
		}
		set
		{
			_module = value;
			Title = (string.IsNullOrWhiteSpace(value?.Name) ? "Параметры модуля" : value.Name);
		}
	}

	public Dictionary<string, object> Values { get; } = new Dictionary<string, object>();

	public Dictionary<string, string> StringValues { get; } = new Dictionary<string, string>();

	public ICommand CancelCommand { get; }

	public event Action<bool?> RequestClose;

	public ModuleArgsViewModel()
	{
		CancelCommand = new RelayCommand(delegate
		{
			this.RequestClose?.Invoke(false);
		});
	}

	public void ConfirmWithValues(Dictionary<string, object> values, Dictionary<string, string> stringValues)
	{
		try
		{
			Values.Clear();
			StringValues.Clear();
			if (values != null)
			{
				foreach (KeyValuePair<string, object> value in values)
				{
					Values[value.Key] = value.Value;
				}
			}
			if (stringValues == null)
			{
				return;
			}
			foreach (KeyValuePair<string, string> stringValue in stringValues)
			{
				StringValues[stringValue.Key] = stringValue.Value;
			}
		}
		catch
		{
		}
		finally
		{
			this.RequestClose?.Invoke(true);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Pw.Hub.Infrastructure;
using Pw.Hub.Services;
using Pw.Hub.Windows;

namespace Pw.Hub.ViewModels;

public class ModulesApiEditorViewModel : INotifyPropertyChanged
{
	public class InputItem : INotifyPropertyChanged
	{
		private string _name = string.Empty;

		private string _label = string.Empty;

		private string _type = "string";

		private string _default = string.Empty;

		private bool _required;

		private string _optionsCsv = string.Empty;

		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				_name = value ?? string.Empty;
				OnPropertyChanged("Name");
			}
		}

		public string Label
		{
			get
			{
				return _label;
			}
			set
			{
				_label = value ?? string.Empty;
				OnPropertyChanged("Label");
			}
		}

		public string Type
		{
			get
			{
				return _type;
			}
			set
			{
				_type = (string.IsNullOrWhiteSpace(value) ? "string" : value.Trim());
				OnPropertyChanged("Type");
			}
		}

		public string Default
		{
			get
			{
				return _default;
			}
			set
			{
				_default = value ?? string.Empty;
				OnPropertyChanged("Default");
			}
		}

		public bool Required
		{
			get
			{
				return _required;
			}
			set
			{
				_required = value;
				OnPropertyChanged("Required");
			}
		}

		public string OptionsCsv
		{
			get
			{
				return _optionsCsv;
			}
			set
			{
				_optionsCsv = value ?? string.Empty;
				OnPropertyChanged("OptionsCsv");
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	private readonly IAiDocService _aiDoc;

	private bool _isBusy;

	private string _name = string.Empty;

	private string _version = "1.0.0";

	private string _description = string.Empty;

	private string _script = string.Empty;

	private InputItem? _selectedInput;

	public bool IsBusy
	{
		get
		{
			return _isBusy;
		}
		private set
		{
			_isBusy = value;
			OnPropertyChanged("IsBusy");
			CommandManager.InvalidateRequerySuggested();
		}
	}

	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			_name = value ?? string.Empty;
			OnPropertyChanged("Name");
			CommandManager.InvalidateRequerySuggested();
		}
	}

	public string Version
	{
		get
		{
			return _version;
		}
		set
		{
			_version = (string.IsNullOrWhiteSpace(value) ? "1.0.0" : value.Trim());
			OnPropertyChanged("Version");
		}
	}

	public string Description
	{
		get
		{
			return _description;
		}
		set
		{
			_description = value ?? string.Empty;
			OnPropertyChanged("Description");
		}
	}

	public string Script
	{
		get
		{
			return _script;
		}
		set
		{
			_script = value ?? string.Empty;
			OnPropertyChanged("Script");
		}
	}

	public ObservableCollection<InputItem> Inputs { get; } = new ObservableCollection<InputItem>();

	public bool IsSaved { get; private set; }

	public ICommand AddInputCommand { get; }

	public ICommand RemoveInputCommand { get; }

	public ICommand SaveCommand { get; }

	public ICommand CancelCommand { get; }

	public ICommand GenerateDescriptionCommand { get; }

	public InputItem? SelectedInput
	{
		get
		{
			return _selectedInput;
		}
		set
		{
			_selectedInput = value;
			OnPropertyChanged("SelectedInput");
			CommandManager.InvalidateRequerySuggested();
		}
	}

	public event Action<bool?> RequestClose;

	public event PropertyChangedEventHandler PropertyChanged;

	public ModulesApiEditorViewModel()
	{
		_aiDoc = (App.Services?.GetService(typeof(IAiDocService)) as IAiDocService) ?? new AiDocService();
		AddInputCommand = new RelayCommand(delegate
		{
			InputItem inputItem = new InputItem
			{
				Name = "param",
				Label = "Параметр",
				Type = "string",
				Default = string.Empty,
				Required = false
			};
			Inputs.Add(inputItem);
			SelectedInput = inputItem;
		});
		RemoveInputCommand = new RelayCommand(delegate
		{
			if (SelectedInput != null)
			{
				int num = Inputs.IndexOf(SelectedInput);
				Inputs.Remove(SelectedInput);
				if (Inputs.Count > 0)
				{
					SelectedInput = Inputs[Math.Clamp(num - 1, 0, Inputs.Count - 1)];
				}
				else
				{
					SelectedInput = null;
				}
			}
		}, (object _) => SelectedInput != null);
		SaveCommand = new RelayCommand(delegate
		{
			IsSaved = true;
			this.RequestClose?.Invoke(true);
		}, (object _) => CanSave());
		CancelCommand = new RelayCommand(delegate
		{
			this.RequestClose?.Invoke(false);
		});
		GenerateDescriptionCommand = new RelayCommand(async delegate
		{
			await GenerateDescriptionAsync();
		}, (object _) => CanGenerateDescription());
	}

	private bool CanSave()
	{
		if (!string.IsNullOrWhiteSpace(Name))
		{
			return !string.IsNullOrWhiteSpace(Script);
		}
		return false;
	}

	private bool CanGenerateDescription()
	{
		if (!IsBusy)
		{
			return !string.IsNullOrWhiteSpace(Name);
		}
		return false;
	}

	private async Task GenerateDescriptionAsync()
	{
		if (!CanGenerateDescription())
		{
			return;
		}
		IAiConfigService aiConfigService = (App.Services?.GetService(typeof(IAiConfigService)) as IAiConfigService) ?? new AiConfigService();
		if (string.IsNullOrWhiteSpace((aiConfigService.GetEffective().ApiKey ?? string.Empty).Trim()))
		{
			try
			{
				IWindowService windowService = App.Services?.GetService(typeof(IWindowService)) as IWindowService;
				AiApiKeyWindow aiApiKeyWindow = new AiApiKeyWindow();
				if (windowService != null)
				{
					Window owner = Application.Current?.MainWindow;
					windowService.ShowDialog(aiApiKeyWindow, owner);
				}
				else
				{
					aiApiKeyWindow.Owner = Application.Current?.MainWindow;
					aiApiKeyWindow.ShowDialog();
				}
			}
			catch
			{
			}
			if (string.IsNullOrWhiteSpace((aiConfigService.GetEffective().ApiKey ?? string.Empty).Trim()))
			{
				Description = (Description ?? string.Empty) + (string.IsNullOrEmpty(Description) ? string.Empty : "\n\n") + "[AI] Укажите API key в настройках AI, чтобы сгенерировать описание.";
				return;
			}
		}
		IsBusy = true;
		try
		{
			List<(string, string, string)> inputs = Inputs.Select((InputItem i) => (i.Name ?? string.Empty, i.Type ?? "string", string.IsNullOrWhiteSpace(i.Label) ? (i.Name ?? string.Empty) : i.Label)).ToList();
			string scriptFragment = Script ?? string.Empty;
			Description = (await _aiDoc.GenerateDescriptionAsync(Name?.Trim() ?? string.Empty, Version?.Trim() ?? "1.0.0", inputs, scriptFragment)) ?? string.Empty;
		}
		catch (Exception ex)
		{
			Description = (Description ?? string.Empty) + (string.IsNullOrEmpty(Description) ? string.Empty : "\n\n") + "[Ошибка генерации описания] " + ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	public CreateOrUpdateModule BuildRequest()
	{
		return new CreateOrUpdateModule
		{
			Name = (Name?.Trim() ?? string.Empty),
			Version = (string.IsNullOrWhiteSpace(Version) ? "1.0.0" : Version.Trim()),
			Description = (Description ?? string.Empty),
			Script = (Script ?? string.Empty),
			Inputs = Inputs.Select((InputItem i) => new InputDefinitionDto
			{
				Name = (i.Name ?? string.Empty),
				Label = (string.IsNullOrWhiteSpace(i.Label) ? (i.Name ?? string.Empty) : i.Label),
				Type = (string.IsNullOrWhiteSpace(i.Type) ? "string" : i.Type),
				Default = i.Default,
				Required = i.Required,
				Options = (from s in (i.OptionsCsv ?? string.Empty).Split(new char[3] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
					select s.Trim() into s
					where !string.IsNullOrWhiteSpace(s)
					select s).Distinct<string>(StringComparer.Ordinal).ToArray()
			}).ToArray()
		};
	}

	private void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Markdig;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Services;
using Pw.Hub.Windows;

namespace Pw.Hub.ViewModels;

public class ModulesLibraryViewModel : INotifyPropertyChanged
{
	private readonly IWindowService _windowService;

	private readonly ModulesApiClient _api;

	private readonly ModuleService _moduleService;

	private readonly IModulesSyncService _modulesSync;

	private ModuleDto _selectedModule;

	private string _searchText;

	private string _tagsText;

	private string _sortOption = "name";

	private string _orderOption = "desc";

	private string _titleText = string.Empty;

	private string _descriptionHtml = string.Empty;

	private bool _isDeveloper;

	private bool _canUpdateAll;

	private bool _canUpdateSelected;

	private bool _isSelectedInstalled;

	public ObservableCollection<ModuleDto> Modules { get; } = new ObservableCollection<ModuleDto>();

	public ICommand CreateModuleCommand { get; private set; }

	public ICommand EditModuleCommand { get; private set; }

	public ICommand DeleteModuleCommand { get; private set; }

	public ICommand OpenLuaEditorCommand { get; private set; }

	public ModuleDto SelectedModule
	{
		get
		{
			return _selectedModule;
		}
		set
		{
			if (_selectedModule != value)
			{
				_selectedModule = value;
				OnPropertyChanged("SelectedModule");
				UpdateDetailsForSelection();
			}
		}
	}

	public string SearchText
	{
		get
		{
			return _searchText;
		}
		set
		{
			_searchText = value;
			OnPropertyChanged("SearchText");
		}
	}

	public string TagsText
	{
		get
		{
			return _tagsText;
		}
		set
		{
			_tagsText = value;
			OnPropertyChanged("TagsText");
		}
	}

	public string SortOption
	{
		get
		{
			return _sortOption;
		}
		set
		{
			_sortOption = value;
			OnPropertyChanged("SortOption");
		}
	}

	public string OrderOption
	{
		get
		{
			return _orderOption;
		}
		set
		{
			_orderOption = value;
			OnPropertyChanged("OrderOption");
		}
	}

	public string TitleText
	{
		get
		{
			return _titleText;
		}
		set
		{
			_titleText = value;
			OnPropertyChanged("TitleText");
		}
	}

	public string DescriptionHtml
	{
		get
		{
			return _descriptionHtml;
		}
		set
		{
			_descriptionHtml = value;
			OnPropertyChanged("DescriptionHtml");
		}
	}

	public bool IsDeveloper
	{
		get
		{
			return _isDeveloper;
		}
		set
		{
			_isDeveloper = value;
			OnPropertyChanged("IsDeveloper");
		}
	}

	public bool CanUpdateAll
	{
		get
		{
			return _canUpdateAll;
		}
		set
		{
			_canUpdateAll = value;
			OnPropertyChanged("CanUpdateAll");
		}
	}

	public bool CanUpdateSelected
	{
		get
		{
			return _canUpdateSelected;
		}
		set
		{
			_canUpdateSelected = value;
			OnPropertyChanged("CanUpdateSelected");
		}
	}

	public bool IsSelectedInstalled
	{
		get
		{
			return _isSelectedInstalled;
		}
		set
		{
			_isSelectedInstalled = value;
			OnPropertyChanged("IsSelectedInstalled");
		}
	}

	public ICommand SearchCommand { get; }

	public ICommand UpdateAllCommand { get; }

	public ICommand UpdateSelectedCommand { get; }

	public ICommand InstallCommand { get; }

	public ICommand UninstallCommand { get; }

	public event PropertyChangedEventHandler PropertyChanged;

	private void TryRefreshOwnerModules()
	{
		try
		{
			(Application.Current?.MainWindow as MainWindow)?.LoadModules();
		}
		catch
		{
		}
	}

	public ModulesLibraryViewModel()
		: this(new WindowService(), new ModulesSyncService())
	{
	}

	public ModulesLibraryViewModel(IWindowService windowService, IModulesSyncService modulesSyncService)
	{
		_windowService = windowService ?? new WindowService();
		_api = new ModulesApiClient();
		_moduleService = new ModuleService();
		_modulesSync = modulesSyncService ?? new ModulesSyncService();
		SearchCommand = new RelayCommand(async delegate
		{
			await SearchAsync();
		});
		UpdateAllCommand = new RelayCommand(async delegate
		{
			await UpdateAllAsync();
		}, (object _) => CanUpdateAll);
		UpdateSelectedCommand = new RelayCommand(async delegate
		{
			await UpdateSelectedAsync();
		}, (object _) => CanUpdateSelected);
		InstallCommand = new RelayCommand(async delegate
		{
			await InstallSelectedAsync();
		}, (object _) => SelectedModule != null && !IsInstalled(SelectedModule.Id));
		UninstallCommand = new RelayCommand(async delegate
		{
			await UninstallSelectedAsync();
		}, (object _) => SelectedModule != null && IsInstalled(SelectedModule.Id));
		CreateModuleCommand = new RelayCommand(delegate
		{
			CreateModule();
		}, (object _) => IsDeveloper);
		EditModuleCommand = new RelayCommand(delegate
		{
			EditModule();
		}, (object _) => IsDeveloper && SelectedModule != null && IsOwnedByCurrentUser(SelectedModule));
		DeleteModuleCommand = new RelayCommand(delegate
		{
			DeleteModule();
		}, (object _) => IsDeveloper && SelectedModule != null && IsOwnedByCurrentUser(SelectedModule));
		OpenLuaEditorCommand = new RelayCommand(delegate
		{
			OpenLuaEditor();
		});
	}

	public async Task InitAsync()
	{
		try
		{
			if (_api.CurrentUser == null)
			{
				await _api.MeAsync();
			}
			IsDeveloper = _api.CurrentUser?.Developer ?? false;
		}
		catch
		{
			IsDeveloper = false;
		}
		await SearchAsync();
	}

	public async Task SearchAsync()
	{
		try
		{
			PagedModulesResponse obj = await _api.SearchAsync(SearchText, TagsText, SortOption, OrderOption, 1, 50);
			Modules.Clear();
			foreach (ModuleDto item in obj.Items)
			{
				Modules.Add(item);
			}
			RecomputeUpdateIndicators();
			if (Modules.Count > 0)
			{
				SelectedModule = Modules[0];
			}
		}
		catch
		{
		}
	}

	private async Task UpdateAllAsync()
	{
		foreach (ModuleDto item in Modules.ToList())
		{
			if (IsUpdateAvailable(GetLocalVersion(item.Id), item.Version ?? "1.0.0"))
			{
				await InstallModuleAsync(item);
			}
		}
		RecomputeUpdateIndicators();
		UpdateSelectedFlags();
		TryRefreshOwnerModules();
	}

	private async Task UpdateSelectedAsync()
	{
		if (SelectedModule != null)
		{
			await InstallModuleAsync(SelectedModule);
			RecomputeUpdateIndicators();
			UpdateSelectedFlags();
			TryRefreshOwnerModules();
		}
	}

	private async Task InstallSelectedAsync()
	{
		if (SelectedModule != null)
		{
			await InstallModuleAsync(SelectedModule);
			RecomputeUpdateIndicators();
			UpdateSelectedFlags();
			TryRefreshOwnerModules();
		}
	}

	private void UninstallSelected()
	{
		if (SelectedModule != null)
		{
			_modulesSync.RemoveModuleLocally(SelectedModule.Id);
			RecomputeUpdateIndicators();
			UpdateSelectedFlags();
			TryRefreshOwnerModules();
		}
	}

	private void UpdateDetailsForSelection()
	{
		ModuleDto selectedModule = SelectedModule;
		TitleText = selectedModule?.Name ?? string.Empty;
		string markdown = selectedModule?.Description ?? string.Empty;
		MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
		string text = Markdown.ToHtml(markdown, pipeline);
		DescriptionHtml = "<html><head><meta charset='utf-8'></head><body style='color:#CCC;background:#1E1E1E;font-family:Segoe UI'>" + text + "</body></html>";
		UpdateSelectedFlags();
	}

	private void RecomputeUpdateIndicators()
	{
		try
		{
			bool canUpdateAll = Modules.Any((ModuleDto m) => IsUpdateAvailable(GetLocalVersion(m.Id), m.Version ?? "1.0.0"));
			CanUpdateAll = canUpdateAll;
		}
		catch
		{
			CanUpdateAll = false;
		}
	}

	private void UpdateSelectedFlags()
	{
		ModuleDto selectedModule = SelectedModule;
		if (selectedModule == null)
		{
			CanUpdateSelected = false;
			IsSelectedInstalled = false;
			return;
		}
		string localVersion = GetLocalVersion(selectedModule.Id);
		CanUpdateSelected = IsUpdateAvailable(localVersion, selectedModule.Version ?? "1.0.0");
		IsSelectedInstalled = IsInstalled(selectedModule.Id);
		CommandManager.InvalidateRequerySuggested();
	}

	private async Task InstallModuleAsync(ModuleDto module)
	{
		_ = 1;
		try
		{
			ModuleDto moduleDto = await _api.GetModuleAsync(module.Id);
			if (moduleDto == null)
			{
				return;
			}
			_modulesSync.InstallModuleLocally(moduleDto);
			try
			{
				if (_api.CurrentUser == null)
				{
					await _api.MeAsync();
				}
				string text = _api.CurrentUser?.UserId ?? string.Empty;
				if (!string.IsNullOrWhiteSpace(text))
				{
					_api.InstallAsync(module.Id, text);
				}
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private async Task UninstallSelectedAsync()
	{
		if (SelectedModule == null)
		{
			return;
		}
		try
		{
			_modulesSync.RemoveModuleLocally(SelectedModule.Id);
			try
			{
				if (_api.CurrentUser == null)
				{
					await _api.MeAsync();
				}
				string text = _api.CurrentUser?.UserId ?? string.Empty;
				if (!string.IsNullOrWhiteSpace(text))
				{
					_api.UninstallAsync(SelectedModule.Id, text);
				}
			}
			catch
			{
			}
		}
		catch
		{
		}
		finally
		{
			RecomputeUpdateIndicators();
			UpdateSelectedFlags();
			TryRefreshOwnerModules();
		}
	}

	private bool IsOwnedByCurrentUser(ModuleDto m)
	{
		try
		{
			string text = _api.CurrentUser?.UserId;
			if (string.IsNullOrWhiteSpace(text) || m == null)
			{
				return false;
			}
			return string.Equals(m.OwnerUserId, text, StringComparison.Ordinal);
		}
		catch
		{
			return false;
		}
	}

	private async void CreateModule()
	{
		try
		{
			ModulesApiEditorWindow editor = new ModulesApiEditorWindow();
			_windowService.ShowWindow(editor);
			editor.Closed += async delegate
			{
				_ = 1;
				try
				{
					if (editor.IsSaved)
					{
						CreateOrUpdateModule request = editor.GetRequest();
						ModuleDto created = await _api.CreateModuleAsync(request);
						if (created != null)
						{
							await SearchAsync();
							SelectedModule = Modules.FirstOrDefault((ModuleDto x) => x.Id == created.Id) ?? created;
						}
					}
				}
				catch (Exception ex2)
				{
					MessageBox.Show("Could not create the module: " + ex2.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
				}
			};
		}
		catch (Exception ex)
		{
			MessageBox.Show("Could not open the editor: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async void EditModule()
	{
		ModuleDto m = SelectedModule;
		if (m == null)
		{
			MessageBox.Show("Select a module.");
			return;
		}
		if (IsOwnedByCurrentUser(m))
		{
			UserDto currentUser = _api.CurrentUser;
			if (currentUser != null && currentUser.Developer)
			{
				try
				{
					ModulesApiEditorWindow editor = new ModulesApiEditorWindow(m);
					_windowService.ShowWindow(editor);
					editor.Closed += async delegate
					{
						_ = 1;
						try
						{
							if (editor.IsSaved)
							{
								CreateOrUpdateModule request = editor.GetRequest();
								ModuleDto updated = await _api.UpdateModuleAsync(m.Id, request);
								if (updated != null)
								{
									await SearchAsync();
									SelectedModule = Modules.FirstOrDefault((ModuleDto x) => x.Id == updated.Id) ?? updated;
								}
							}
						}
						catch (Exception ex2)
						{
							MessageBox.Show("Could not update the module: " + ex2.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
						}
					};
					return;
				}
				catch (Exception ex)
				{
					MessageBox.Show("Could not open the editor: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
					return;
				}
			}
		}
		MessageBox.Show("You can edit only your own modules.");
	}

	private async void DeleteModule()
	{
		ModuleDto selectedModule = SelectedModule;
		if (selectedModule == null)
		{
			return;
		}
		if (IsOwnedByCurrentUser(selectedModule))
		{
			UserDto currentUser = _api.CurrentUser;
			if (currentUser != null && currentUser.Developer)
			{
				if (MessageBox.Show("Delete this module?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
				{
					return;
				}
				try
				{
					if (await _api.DeleteModuleAsync(selectedModule.Id))
					{
						await SearchAsync();
					}
					return;
				}
				catch (Exception ex)
				{
					MessageBox.Show("Could not delete the module: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
					return;
				}
			}
		}
		MessageBox.Show("You can delete only your own modules.");
	}

	private void OpenLuaEditor()
	{
		try
		{
			Window window = Application.Current?.MainWindow;
			LuaEditorWindow window2 = new LuaEditorWindow((window as MainWindow)?.AccountPage?.LuaRunner)
			{
				Owner = window
			};
			_windowService.ShowWindow(window2);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Could not open the Lua editor: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private string GetLocalVersion(Guid id)
	{
		try
		{
			return _moduleService.LoadModules().FirstOrDefault((ModuleDefinition x) => string.Equals(x.Id, id.ToString(), StringComparison.OrdinalIgnoreCase))?.Version;
		}
		catch
		{
			return null;
		}
	}

	private bool IsInstalled(Guid id)
	{
		try
		{
			return (_moduleService.LoadModules() ?? new List<ModuleDefinition>()).Any((ModuleDefinition x) => string.Equals(x.Id, id.ToString(), StringComparison.OrdinalIgnoreCase));
		}
		catch
		{
			return false;
		}
	}

	private static bool IsUpdateAvailable(string local, string server)
	{
		if (local == null || server == null)
		{
			return false;
		}
		try
		{
			int[] array = ParseVersion(local);
			int[] array2 = ParseVersion(server);
			for (int i = 0; i < Math.Max(array.Length, array2.Length); i++)
			{
				int num = ((i < array.Length) ? array[i] : 0);
				int num2 = ((i < array2.Length) ? array2[i] : 0);
				if (num != num2)
				{
					return num < num2;
				}
			}
			return false;
		}
		catch
		{
			return !string.Equals(local, server, StringComparison.OrdinalIgnoreCase);
		}
	}

	private static int[] ParseVersion(string v)
	{
		int result;
		return (from s in (v ?? "0").Split('.', StringSplitOptions.RemoveEmptyEntries)
			select int.TryParse(s, out result) ? result : 0).ToArray();
	}

	private void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

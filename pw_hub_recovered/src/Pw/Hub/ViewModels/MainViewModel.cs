using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Pages;
using Pw.Hub.Services;
using Pw.Hub.Windows;

namespace Pw.Hub.ViewModels;

public class MainViewModel : BaseViewModel
{
	private ModuleDefinition _selectedQuickModule;

	private readonly IWindowService _windowService;

	private readonly IUpdatesCheckService _updatesCheckService;

	private readonly IRunModuleCoordinator _runCoordinator;

	private readonly IUiDialogService _dialogs;

	private readonly IAccountsService _accounts;

	private readonly ModuleService _moduleService = new ModuleService();

	private DateTime _lastUpdateCheck = DateTime.MinValue;

	private List<string> _lastUpdates = new List<string>();

	private bool _isUpdatesPopupOpen;

	private string _updatesText = string.Empty;

	public ObservableCollection<ModuleDefinition> QuickModules { get; } = new ObservableCollection<ModuleDefinition>();

	public ModuleDefinition SelectedQuickModule
	{
		get
		{
			return _selectedQuickModule;
		}
		set
		{
			_selectedQuickModule = value;
			OnPropertyChanged("SelectedQuickModule");
		}
	}

	public ObservableCollection<Squad> Squads { get; } = new ObservableCollection<Squad>();

	public bool IsUpdatesPopupOpen
	{
		get
		{
			return _isUpdatesPopupOpen;
		}
		set
		{
			_isUpdatesPopupOpen = value;
			OnPropertyChanged("IsUpdatesPopupOpen");
		}
	}

	public string UpdatesText
	{
		get
		{
			return _updatesText;
		}
		set
		{
			_updatesText = value;
			OnPropertyChanged("UpdatesText");
		}
	}

	public ICommand OpenModulesLibraryCommand { get; }

	public ICommand OpenProfileCommand { get; }

	public ICommand CloseUpdatesPopupCommand { get; }

	public ICommand RunSelectedModuleCommand { get; }

	public ICommand LoadCharactersCommand { get; }

	public ICommand AddSquadCommand { get; }

	public ICommand AddAccountCommand { get; }

	public ICommand EditSquadCommand { get; }

	public ICommand EditAccountCommand { get; }

	public ICommand DeleteNodeCommand { get; }

	public event Action<Account> RequestLoadCharacters;

	public event Action<object> RequestSelectTreeItem;

	public MainViewModel(IWindowService windowService, IUpdatesCheckService updatesCheckService, IRunModuleCoordinator runCoordinator, IUiDialogService dialogs, IAccountsService accounts)
	{
		_windowService = windowService;
		_updatesCheckService = updatesCheckService;
		_runCoordinator = runCoordinator;
		_dialogs = dialogs ?? new UiDialogService();
		_accounts = accounts ?? new AccountsService();
		OpenModulesLibraryCommand = new RelayCommand(delegate
		{
			OpenModulesLibrary();
		});
		OpenProfileCommand = new RelayCommand(delegate
		{
			OpenProfile();
		});
		CloseUpdatesPopupCommand = new RelayCommand(delegate
		{
			IsUpdatesPopupOpen = false;
		});
		RunSelectedModuleCommand = new RelayCommand(async delegate
		{
			await RunSelectedModuleAsync();
		}, (object _) => SelectedQuickModule != null);
		LoadCharactersCommand = new RelayCommand(delegate(object p)
		{
			OnLoadCharactersRequested(p);
		}, (object p) => p is Account);
		AddSquadCommand = new RelayCommand(delegate
		{
			AddSquad();
		});
		AddAccountCommand = new RelayCommand(delegate(object p)
		{
			AddAccount(p);
		}, (object p) => p is Squad || p is Account);
		EditSquadCommand = new RelayCommand(delegate(object p)
		{
			EditSquad(p as Squad);
		}, (object p) => p is Squad);
		EditAccountCommand = new RelayCommand(delegate(object p)
		{
			EditAccount(p as Account);
		}, (object p) => p is Account);
		DeleteNodeCommand = new RelayCommand(delegate(object p)
		{
			DeleteNode(p);
		}, (object p) => p is Squad || p is Account);
		LoadData();
		LoadQuickModules();
	}

	private async void AddAccount(object parameter)
	{
		try
		{
			Squad squad = null;
			if (parameter is Squad squad2)
			{
				squad = squad2;
			}
			else
			{
				Account acc = parameter as Account;
				if (acc != null)
				{
					squad = Squads.FirstOrDefault((Squad q) => q.Accounts.Any((Account a) => a.Id == acc.Id));
				}
			}
			if (squad == null)
			{
				return;
			}
			CreateAccountWindow createAccountWindow = new CreateAccountWindow();
			if (_windowService.ShowDialog(createAccountWindow) != true)
			{
				return;
			}
			Account obj = await _accounts.CreateAccountAsync(squad, createAccountWindow.AccountName);
			Reload();
			try
			{
				this.RequestSelectTreeItem?.Invoke(obj);
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private async void EditSquad(Squad squad)
	{
		if (squad == null)
		{
			return;
		}
		try
		{
			EditSquadWindow dlg = new EditSquadWindow(squad);
			if (_windowService.ShowDialog(dlg) == true)
			{
				await _accounts.UpdateSquadAsync(squad, dlg.SquadName);
				if (!string.Equals(squad.Name, dlg.SquadName, StringComparison.Ordinal))
				{
					squad.Name = dlg.SquadName;
				}
			}
		}
		catch
		{
		}
	}

	private async void EditAccount(Account account)
	{
		if (account == null)
		{
			return;
		}
		try
		{
			EditAccountWindow dlg = new EditAccountWindow(account);
			if (_windowService.ShowDialog(dlg) == true)
			{
				await _accounts.UpdateAccountAsync(account, dlg.AccountName);
				if (!string.Equals(account.Name, dlg.AccountName, StringComparison.Ordinal))
				{
					account.Name = dlg.AccountName;
				}
			}
		}
		catch
		{
		}
	}

	private void DeleteNode(object parameter)
	{
		try
		{
			if (parameter is Squad squad)
			{
				if (_dialogs.Confirm("Вы действительно хотите удалить отряд \"" + squad.Name + "\"?\nВсе аккаунты в этом отряде также будут удалены.", "Подтверждение удаления"))
				{
					_accounts.DeleteSquadAsync(squad);
					Reload();
				}
			}
			else if (parameter is Account account && _dialogs.Confirm("Вы действительно хотите удалить аккаунт \"" + account.Name + "\"?", "Подтверждение удаления"))
			{
				_accounts.DeleteAccountAsync(account);
				Reload();
			}
		}
		catch
		{
		}
	}

	public MainViewModel()
		: this(new WindowService(), new UpdatesCheckService(), new RunModuleCoordinator(new WindowService(), new LuaExecutionService()), new UiDialogService(), new AccountsService())
	{
	}

	public void ReloadQuickModules()
	{
		LoadQuickModules();
	}

	private void LoadQuickModules()
	{
		try
		{
			List<ModuleDefinition> obj = _moduleService.LoadModules() ?? new List<ModuleDefinition>();
			QuickModules.Clear();
			foreach (ModuleDefinition item in obj)
			{
				QuickModules.Add(item);
			}
			if (SelectedQuickModule == null)
			{
				SelectedQuickModule = QuickModules.FirstOrDefault();
			}
		}
		catch
		{
		}
		finally
		{
			CommandManager.InvalidateRequerySuggested();
		}
	}

	private async Task RunSelectedModuleAsync()
	{
		ModuleDefinition selectedQuickModule = SelectedQuickModule;
		if (selectedQuickModule == null)
		{
			return;
		}
		try
		{
			await _runCoordinator.RunAsync(selectedQuickModule);
		}
		catch
		{
		}
	}

	private void OnLoadCharactersRequested(object parameter)
	{
		if (parameter is Account obj)
		{
			try
			{
				this.RequestLoadCharacters?.Invoke(obj);
			}
			catch
			{
			}
		}
	}

	private void OpenModulesLibrary()
	{
		try
		{
			ModulesLibraryWindow window = new ModulesLibraryWindow();
			_windowService.ShowWindow(window);
			IsUpdatesPopupOpen = false;
		}
		catch
		{
		}
	}

	private void OpenProfile()
	{
		try
		{
			ProfileEditWindow window = new ProfileEditWindow();
			_windowService.ShowDialog(window);
		}
		catch
		{
		}
	}

	private void LoadData()
	{
		List<Squad> list = _accounts.LoadSquads();
		Squads.Clear();
		foreach (Squad item in list)
		{
			Squads.Add(item);
		}
	}

	public void Reload()
	{
		LoadData();
	}

	private async void AddSquad()
	{
		try
		{
			CreateSquadWindow createSquadWindow = new CreateSquadWindow();
			if (_windowService.ShowDialog(createSquadWindow) == true)
			{
				Squad obj = await _accounts.CreateSquadAsync(createSquadWindow.SquadName);
				Reload();
				try
				{
					this.RequestSelectTreeItem?.Invoke(obj);
					return;
				}
				catch
				{
					return;
				}
			}
		}
		catch
		{
		}
	}

	public async Task CheckUpdatesDebouncedAsync(bool force = false)
	{
		try
		{
			if (!force && DateTime.UtcNow - _lastUpdateCheck < TimeSpan.FromSeconds(60.0))
			{
				return;
			}
			_lastUpdateCheck = DateTime.UtcNow;
			List<string> list = await _updatesCheckService.GetUpdatesAsync();
			if (list == null)
			{
				list = new List<string>();
			}
			if (_lastUpdates.SequenceEqual(list))
			{
				return;
			}
			_lastUpdates = list;
			if (list.Count > 0)
			{
				string text = string.Join(Environment.NewLine, list.Take(5));
				if (list.Count > 5)
				{
					text += $" и ещё {list.Count - 5}";
				}
				UpdatesText = "Доступны обновления для модулей: " + Environment.NewLine + " " + text;
				IsUpdatesPopupOpen = true;
			}
			else
			{
				IsUpdatesPopupOpen = false;
			}
		}
		catch
		{
			IsUpdatesPopupOpen = false;
		}
	}
}

using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using NLua;
using Pw.Hub.Controls;
using Pw.Hub.Models;
using Pw.Hub.Pages;
using Pw.Hub.Services;
using Pw.Hub.Tools;
using Pw.Hub.ViewModels;
using Pw.Hub.Windows;

namespace Pw.Hub;

public class MainWindow : Window, IComponentConnector
{
	private class JsOption
	{
		public string value { get; set; } = string.Empty;

		public string text { get; set; } = string.Empty;
	}

	private readonly MainViewModel _vm;

	private readonly ModuleService _moduleService = new ModuleService();

	private List<ModuleDefinition> _modules = new List<ModuleDefinition>();

	private readonly ModulesApiClient _modulesApi = new ModulesApiClient();

	private readonly IModulesSyncService _modulesSync = (App.Services.GetService(typeof(IModulesSyncService)) as IModulesSyncService) ?? new ModulesSyncService();

	private readonly ILuaExecutionService _luaExec = (App.Services.GetService(typeof(ILuaExecutionService)) as ILuaExecutionService) ?? new LuaExecutionService();

	private readonly ICharactersLoadService _charsLoad = (App.Services.GetService(typeof(ICharactersLoadService)) as ICharactersLoadService) ?? new CharactersLoadService();

	private readonly IOrderingService _ordering = (App.Services.GetService(typeof(IOrderingService)) as IOrderingService) ?? new OrderingService();

	internal Grid RootGrid;

	internal ComboBox ModulesList;

	internal Button RunModuleButton;

	internal TreeView NavigationTree;

	internal ContextMenu NavigationContextMenu;

	internal MenuItem AddAccountMenuItem;

	internal MenuItem EditSquadMenuItem;

	internal MenuItem EditAccountMenuItem;

	internal MenuItem LoadCharactersMenuItem;

	internal MenuItem DeleteMenuItem;

	internal Border EmptyState;

	internal AccountPage AccountPage;

	internal UniformGrid BrowserWorkspace;

	internal TextBlock StatusBarText;

	internal Popup UpdatesPopup;

	internal TextBlock UpdatesPopupText;

	private bool _contentLoaded;

	public BrowserManager BrowserManager { get; private set; }

	public void AddBrowserView(BrowserView view)
	{
		try
		{
			if (view == null)
			{
				return;
			}
			if (!base.Dispatcher.CheckAccess())
			{
				base.Dispatcher.Invoke(delegate
				{
					AddBrowserView(view);
				});
				return;
			}
			BrowserWorkspace.Visibility = Visibility.Visible;
			BrowserWorkspace.Children.Add(view);
			try
			{
				if (AccountPage != null)
				{
					AccountPage.Visibility = Visibility.Collapsed;
					AccountPage.IsHitTestVisible = false;
				}
			}
			catch
			{
			}
			UpdateBrowserWorkspaceGrid();
		}
		catch
		{
		}
	}

	public void RemoveBrowserView(BrowserView view)
	{
		try
		{
			if (view == null)
			{
				return;
			}
			if (!base.Dispatcher.CheckAccess())
			{
				base.Dispatcher.Invoke(delegate
				{
					RemoveBrowserView(view);
				});
				return;
			}
			BrowserWorkspace.Children.Remove(view);
			if (BrowserWorkspace.Children.Count == 0)
			{
				BrowserWorkspace.Visibility = Visibility.Collapsed;
				try
				{
					if (AccountPage != null)
					{
						AccountPage.Visibility = Visibility.Visible;
						AccountPage.IsHitTestVisible = true;
					}
				}
				catch
				{
				}
			}
			UpdateBrowserWorkspaceGrid();
		}
		catch
		{
		}
	}

	public void UpdateBrowserWorkspaceGrid()
	{
		try
		{
			if (!base.Dispatcher.CheckAccess())
			{
				base.Dispatcher.Invoke(UpdateBrowserWorkspaceGrid);
				return;
			}
			int count = BrowserWorkspace.Children.Count;
			if (count <= 0)
			{
				BrowserWorkspace.Rows = 1;
				BrowserWorkspace.Columns = 1;
				return;
			}
			int num = (int)Math.Ceiling(Math.Sqrt(count));
			int rows = (int)Math.Ceiling((double)count / (double)num);
			BrowserWorkspace.Columns = num;
			BrowserWorkspace.Rows = rows;
		}
		catch
		{
		}
	}

	public MainWindow()
	{
		InitializeComponent();
		try
		{
			ProfileCleanupService.CleanupLeftoversAsync(TimeSpan.FromHours(12.0));
		}
		catch
		{
		}
		BrowserManager = new BrowserManager(this);
		_vm = (App.Services.GetService(typeof(MainViewModel)) as MainViewModel) ?? new MainViewModel();
		base.DataContext = _vm;
		try
		{
			_vm.RequestLoadCharacters += OnRequestLoadCharacters;
		}
		catch
		{
		}
		try
		{
			_vm.RequestSelectTreeItem += OnRequestSelectTreeItem;
		}
		catch
		{
		}
		try
		{
			base.SizeChanged += delegate
			{
				try
				{
					if (_vm.IsUpdatesPopupOpen)
					{
						PositionUpdatesPopup();
					}
				}
				catch
				{
				}
			};
			base.LocationChanged += delegate
			{
				try
				{
					if (_vm.IsUpdatesPopupOpen)
					{
						PositionUpdatesPopup();
					}
				}
				catch
				{
				}
			};
			base.StateChanged += delegate
			{
				try
				{
					if (_vm.IsUpdatesPopupOpen)
					{
						PositionUpdatesPopup();
					}
				}
				catch
				{
				}
			};
		}
		catch
		{
		}
		try
		{
			AccountPage.AccountManager.CurrentAccountChanged += OnCurrentAccountChanged;
			AccountPage.AccountManager.CurrentAccountDataChanged += OnCurrentAccountDataChanged;
			AccountPage.AccountManager.CurrentAccountChanging += OnCurrentAccountChanging;
		}
		catch
		{
		}
		base.Loaded += async delegate
		{
			try
			{
				LoadModules();
			}
			catch
			{
			}
			try
			{
				await SyncInstalledModulesAsync();
			}
			catch
			{
			}
		};
	}

	private async void OnRequestSelectTreeItem(object item)
	{
		try
		{
			await base.Dispatcher.InvokeAsync((Func<Task>)async delegate
			{
				await SelectInNavigationTreeAsync(item);
			}, DispatcherPriority.Background);
		}
		catch
		{
		}
	}

	private async Task SelectInNavigationTreeAsync(object item)
	{
		if (item == null)
		{
			return;
		}
		try
		{
			AccountPage accountPage = AccountPage;
			if (accountPage != null && accountPage.AccountManager?.IsChanging == true)
			{
				return;
			}
		}
		catch
		{
		}
		Account acc = item as Account;
		if (acc != null)
		{
			Squad squad = _vm.Squads.FirstOrDefault((Squad s) => s.Id == acc.SquadId);
			if (squad == null)
			{
				return;
			}
			Account targetAccount = squad.Accounts.FirstOrDefault((Account a) => a.Id == acc.Id);
			if (targetAccount == null)
			{
				return;
			}
			TreeViewItem squadContainer = await GetContainerAsync(NavigationTree, squad);
			if (squadContainer != null)
			{
				squadContainer.IsExpanded = true;
				await Task.Yield();
				TreeViewItem treeViewItem = await GetContainerAsync(squadContainer, targetAccount);
				if (treeViewItem != null)
				{
					treeViewItem.IsSelected = true;
					treeViewItem.Focus();
					treeViewItem.BringIntoView();
				}
			}
			return;
		}
		Squad sq = item as Squad;
		if (sq == null)
		{
			return;
		}
		Squad squad2 = _vm.Squads.FirstOrDefault((Squad s) => s.Id == sq.Id);
		if (squad2 != null)
		{
			TreeViewItem treeViewItem2 = await GetContainerAsync(NavigationTree, squad2);
			if (treeViewItem2 != null)
			{
				treeViewItem2.IsExpanded = true;
				treeViewItem2.IsSelected = true;
				treeViewItem2.Focus();
				treeViewItem2.BringIntoView();
			}
		}
	}

	private async Task<TreeViewItem> GetContainerAsync(ItemsControl parent, object item)
	{
		for (int i = 0; i < 10; i++)
		{
			ItemContainerGenerator itemContainerGenerator = parent?.ItemContainerGenerator;
			if (itemContainerGenerator != null)
			{
				if (itemContainerGenerator.ContainerFromItem(item) is TreeViewItem result)
				{
					return result;
				}
				if (itemContainerGenerator.Status == GeneratorStatus.ContainersGenerated && itemContainerGenerator.ContainerFromItem(item) is TreeViewItem result2)
				{
					return result2;
				}
			}
			await Task.Delay(25);
			try
			{
				parent.UpdateLayout();
			}
			catch
			{
			}
		}
		return null;
	}

	private async Task SyncInstalledModulesAsync()
	{
		try
		{
			if (await _modulesSync.SyncInstalledAsync())
			{
				try
				{
					LoadModules();
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

	private void InstallModuleLocally(ModuleDto module)
	{
		try
		{
			_modulesSync.InstallModuleLocally(module);
		}
		catch
		{
		}
	}

	private void RemoveModuleLocally(Guid id)
	{
		try
		{
			_modulesSync.RemoveModuleLocally(id);
		}
		catch
		{
		}
	}

	private void OpenProfile_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			ProfileEditWindow profileEditWindow = new ProfileEditWindow();
			profileEditWindow.Owner = this;
			profileEditWindow.ShowDialog();
		}
		catch
		{
		}
	}

	private void OnCurrentAccountChanged(Account account)
	{
		if (account == null)
		{
			return;
		}
		base.Dispatcher.InvokeAsync(delegate
		{
			try
			{
				Squad squad = _vm.Squads.FirstOrDefault((Squad s) => s.Accounts.Any((Account a) => a.Id == account.Id));
				if (squad != null)
				{
					Account account2 = squad.Accounts.FirstOrDefault((Account a) => a.Id == account.Id);
					if (account2 != null)
					{
						NavigationTree.UpdateLayout();
						if (NavigationTree.ItemContainerGenerator.ContainerFromItem(squad) is TreeViewItem treeViewItem)
						{
							treeViewItem.IsExpanded = true;
							treeViewItem.UpdateLayout();
							if (treeViewItem.ItemContainerGenerator.ContainerFromItem(account2) is TreeViewItem treeViewItem2)
							{
								treeViewItem2.IsSelected = true;
								treeViewItem2.BringIntoView();
							}
						}
					}
				}
			}
			catch
			{
			}
		});
	}

	private void OnCurrentAccountDataChanged(Account account)
	{
		if (account == null)
		{
			return;
		}
		base.Dispatcher.InvokeAsync(delegate
		{
			try
			{
				Squad squad = _vm.Squads.FirstOrDefault((Squad s) => s.Accounts.Any((Account a) => a.Id == account.Id));
				if (squad != null)
				{
					Account account2 = squad.Accounts.FirstOrDefault((Account a) => a.Id == account.Id);
					if (account2 != null)
					{
						if (!string.Equals(account2.ImageSource, account.ImageSource, StringComparison.Ordinal))
						{
							account2.ImageSource = account.ImageSource;
						}
						if (!string.Equals(account2.SiteId, account.SiteId, StringComparison.Ordinal))
						{
							account2.SiteId = account.SiteId;
						}
						if (!string.Equals(account2.Name, account.Name, StringComparison.Ordinal))
						{
							account2.Name = account.Name;
						}
					}
				}
			}
			catch
			{
			}
		});
	}

	private void OnCurrentAccountChanging(bool isChanging)
	{
		try
		{
			base.Dispatcher.InvokeAsync(delegate
			{
				try
				{
					if (NavigationTree != null)
					{
						NavigationTree.IsHitTestVisible = !isChanging;
						NavigationTree.Focusable = !isChanging;
					}
					if (StatusBarText != null)
					{
						StatusBarText.Text = (isChanging ? "Смена аккаунта..." : string.Empty);
					}
				}
				catch
				{
				}
			});
		}
		catch
		{
		}
	}

	private static void CollapseSiblings(TreeViewItem item)
	{
		ItemsControl itemsControl = ItemsControl.ItemsControlFromItemContainer(item);
		if (itemsControl == null)
		{
			return;
		}
		foreach (object item2 in (IEnumerable)itemsControl.Items)
		{
			if (itemsControl.ItemContainerGenerator.ContainerFromItem(item2) is TreeViewItem treeViewItem && treeViewItem != item)
			{
				treeViewItem.IsExpanded = false;
			}
		}
	}

	private void NavigationTree_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (AccountPage.IsCoreInitialized && !(e.OriginalSource is ToggleButton) && sender is TreeView treeView && treeView.ItemContainerGenerator.ContainerFromItem(treeView.SelectedItem) is TreeViewItem treeViewItem)
		{
			treeViewItem.IsExpanded = !treeViewItem.IsExpanded;
		}
	}

	private void NavigationTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
	{
		try
		{
			ControlsList_SelectedItemChanged();
		}
		catch
		{
		}
	}

	private void NavigationTree_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
	{
		try
		{
			DependencyObject dependencyObject = e.OriginalSource as DependencyObject;
			while (dependencyObject != null && !(dependencyObject is TreeViewItem))
			{
				dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			}
			if (dependencyObject is TreeViewItem treeViewItem)
			{
				treeViewItem.IsSelected = true;
				treeViewItem.Focus();
			}
		}
		catch
		{
		}
	}

	private void SelectedItemChanged(TreeViewItem tvi)
	{
		ControlsList_SelectedItemChanged();
		if (tvi != null)
		{
			tvi.IsExpanded = !tvi.IsExpanded;
		}
	}

	private async void ControlsList_SelectedItemChanged()
	{
		try
		{
			AccountPage accountPage = AccountPage;
			if (accountPage != null && accountPage.AccountManager?.IsChanging == true)
			{
				return;
			}
		}
		catch
		{
		}
		if (NavigationTree.SelectedItem is Account account)
		{
			if (AccountPage.AccountManager.CurrentAccount?.Id != account.Id)
			{
				await AccountPage.AccountManager.ChangeAccountAsync(account.Id);
			}
		}
		else if (NavigationTree.SelectedItem is Squad item && NavigationTree.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
		{
			treeViewItem.BringIntoView();
			CollapseSiblings(treeViewItem);
		}
	}

	private void MainWindow_OnClosing(object sender, CancelEventArgs e)
	{
		e.Cancel = true;
		if (_vm.IsUpdatesPopupOpen)
		{
			_vm.IsUpdatesPopupOpen = false;
		}
		try
		{
			_vm.RequestLoadCharacters -= OnRequestLoadCharacters;
		}
		catch
		{
		}
		try
		{
			_vm.RequestSelectTreeItem -= OnRequestSelectTreeItem;
		}
		catch
		{
		}
		Hide();
	}

	private async void MainWindow_OnActivated(object sender, EventArgs e)
	{
		try
		{
			await _vm.CheckUpdatesDebouncedAsync();
			if (_vm.IsUpdatesPopupOpen)
			{
				try
				{
					PositionUpdatesPopup();
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

	private void UpdatesPopup_CloseClick(object sender, RoutedEventArgs e)
	{
		try
		{
			_vm.IsUpdatesPopupOpen = false;
		}
		catch
		{
		}
	}

	private void MainWindow_OnDeactivated(object sender, EventArgs e)
	{
		try
		{
			if (_vm.IsUpdatesPopupOpen)
			{
				_vm.IsUpdatesPopupOpen = false;
			}
		}
		catch
		{
		}
	}

	private void MainWindow_OnStateChanged(object sender, EventArgs e)
	{
		try
		{
			if (base.WindowState == WindowState.Minimized)
			{
				if (_vm.IsUpdatesPopupOpen)
				{
					_vm.IsUpdatesPopupOpen = false;
				}
			}
			else if (_vm.IsUpdatesPopupOpen)
			{
				PositionUpdatesPopup();
			}
		}
		catch
		{
		}
	}

	private void PositionUpdatesPopup()
	{
		try
		{
			if (RootGrid == null || UpdatesPopup == null)
			{
				return;
			}
			try
			{
				UpdatesPopup.Placement = PlacementMode.AbsolutePoint;
				UpdatesPopup.PlacementTarget = null;
			}
			catch
			{
			}
			FrameworkElement frameworkElement = UpdatesPopup.Child as FrameworkElement;
			double num = ((frameworkElement != null && frameworkElement.ActualWidth > 0.0) ? frameworkElement.ActualWidth : 320.0);
			double num2 = ((frameworkElement != null && frameworkElement.ActualHeight > 0.0) ? frameworkElement.ActualHeight : 140.0);
			double actualWidth = RootGrid.ActualWidth;
			double actualHeight = RootGrid.ActualHeight;
			if (!(actualWidth <= 0.0) && !(actualHeight <= 0.0))
			{
				Point point = new Point(Math.Max(0.0, actualWidth - num - 30.0), Math.Max(0.0, actualHeight - num2 - 40.0));
				Point point2 = RootGrid.PointToScreen(point);
				PresentationSource presentationSource = PresentationSource.FromVisual(this);
				if (presentationSource?.CompositionTarget != null)
				{
					Point point3 = presentationSource.CompositionTarget.TransformFromDevice.Transform(new Point(point2.X, point2.Y));
					UpdatesPopup.HorizontalOffset = point3.X;
					UpdatesPopup.VerticalOffset = point3.Y;
				}
				else
				{
					UpdatesPopup.HorizontalOffset = point2.X;
					UpdatesPopup.VerticalOffset = point2.Y;
				}
			}
		}
		catch
		{
		}
	}

	private static string NormalizeSemVer(string v)
	{
		if (string.IsNullOrWhiteSpace(v))
		{
			return "0.0.0";
		}
		return v.Split('+')[0].Split('-')[0].Trim();
	}

	private static bool TryParseVersion(string v, out Version ver)
	{
		return Version.TryParse(NormalizeSemVer(v), out ver);
	}

	private static bool IsUpdateAvailable(string local, string server)
	{
		if (TryParseVersion(local, out var ver) && TryParseVersion(server, out var ver2))
		{
			return ver2 > ver;
		}
		return !string.Equals(local, server, StringComparison.Ordinal);
	}

	public void LoadModules()
	{
		try
		{
			_modules = _moduleService.LoadModules();
			try
			{
				_vm.ReloadQuickModules();
			}
			catch
			{
			}
			Button runModuleButton = RunModuleButton;
			List<ModuleDefinition> modules = _modules;
			runModuleButton.IsEnabled = modules != null && modules.Count > 0;
		}
		catch
		{
		}
	}

	private async void Test_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			using Lua lua = new Lua();
			lua.State.Encoding = Encoding.UTF8;
			TaskCompletionSource<string> taskCompletionSource = new TaskCompletionSource<string>();
			LuaIntegration target = new LuaIntegration(AccountPage.AccountManager, taskCompletionSource);
			MethodInfo method = typeof(LuaIntegration).GetMethod("GetAccountAsyncCallback", new Type[1] { typeof(LuaFunction) });
			if (method == null)
			{
				MessageBox.Show("Не удалось найти метод интеграции Lua.");
				return;
			}
			lua.RegisterFunction("GetAccountAsyncCallback", target, method);
			lua.DoString("\r\n                GetAccountAsyncCallback(function(accountName)\r\n                    return 'Hello from Lua, ' .. accountName .. '!'\r\n                end)\r\n            ");
			MessageBox.Show(await taskCompletionSource.Task.ConfigureAwait(continueOnCapturedContext: true));
		}
		catch (Exception ex)
		{
			MessageBox.Show("Ошибка Lua: " + ex.Message);
		}
	}

	private void OpenModulesLibrary_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			ModulesLibraryWindow modulesLibraryWindow = new ModulesLibraryWindow();
			modulesLibraryWindow.Owner = this;
			modulesLibraryWindow.Show();
			try
			{
				if (_vm.IsUpdatesPopupOpen)
				{
					_vm.IsUpdatesPopupOpen = false;
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

	private async void OnLoadCharactersClick(object sender, RoutedEventArgs e)
	{
		await Task.CompletedTask;
	}

	private async void OnRequestLoadCharacters(Account account)
	{
		if (account == null)
		{
			return;
		}
		try
		{
			await _charsLoad.LoadForAccountAsync(account, AccountPage, this);
			object obj = null;
			try
			{
				obj = NavigationTree?.SelectedItem;
			}
			catch
			{
			}
			try
			{
				_vm.Reload();
			}
			catch
			{
			}
			try
			{
				if (obj is Account account2)
				{
					Account item = new Account
					{
						Id = account2.Id,
						SquadId = account2.SquadId
					};
					await SelectInNavigationTreeAsync(item);
				}
				else if (obj is Squad squad)
				{
					Squad item2 = new Squad
					{
						Id = squad.Id
					};
					await SelectInNavigationTreeAsync(item2);
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

	private void OnGlobalCommandExecuted(object sender, ExecutedRoutedEventArgs e)
	{
		try
		{
			if (string.Equals((e?.Parameter as string)?.Trim(), "ToggleLogs", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					LogsWindow.Toggle(this);
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

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/Pw.Hub;component/mainwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			((MainWindow)target).Closing += MainWindow_OnClosing;
			((MainWindow)target).Activated += MainWindow_OnActivated;
			((MainWindow)target).Deactivated += MainWindow_OnDeactivated;
			((MainWindow)target).StateChanged += MainWindow_OnStateChanged;
			break;
		case 2:
			((CommandBinding)target).Executed += OnGlobalCommandExecuted;
			break;
		case 3:
			RootGrid = (Grid)target;
			break;
		case 4:
			ModulesList = (ComboBox)target;
			break;
		case 5:
			RunModuleButton = (Button)target;
			break;
		case 6:
			NavigationTree = (TreeView)target;
			NavigationTree.SelectedItemChanged += NavigationTree_OnSelectedItemChanged;
			NavigationTree.PreviewMouseLeftButtonUp += NavigationTree_OnPreviewMouseLeftButtonUp;
			NavigationTree.PreviewMouseRightButtonDown += NavigationTree_OnPreviewMouseRightButtonDown;
			break;
		case 7:
			NavigationContextMenu = (ContextMenu)target;
			break;
		case 8:
			AddAccountMenuItem = (MenuItem)target;
			break;
		case 9:
			EditSquadMenuItem = (MenuItem)target;
			break;
		case 10:
			EditAccountMenuItem = (MenuItem)target;
			break;
		case 11:
			LoadCharactersMenuItem = (MenuItem)target;
			break;
		case 12:
			DeleteMenuItem = (MenuItem)target;
			break;
		case 13:
			EmptyState = (Border)target;
			break;
		case 14:
			AccountPage = (AccountPage)target;
			break;
		case 15:
			BrowserWorkspace = (UniformGrid)target;
			break;
		case 16:
			StatusBarText = (TextBlock)target;
			break;
		case 17:
			UpdatesPopup = (Popup)target;
			break;
		case 18:
			UpdatesPopupText = (TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

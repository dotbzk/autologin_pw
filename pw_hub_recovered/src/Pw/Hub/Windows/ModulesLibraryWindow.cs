using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Markdig;
using Microsoft.Web.WebView2.Wpf;
using Pw.Hub.Models;
using Pw.Hub.Services;
using Pw.Hub.ViewModels;

namespace Pw.Hub.Windows;

public class ModulesLibraryWindow : Window, IComponentConnector
{
	private readonly ModulesLibraryViewModel _vm;

	private readonly ModulesApiClient _api = new ModulesApiClient();

	private ModuleDto _selected;

	private string _userId;

	private readonly ModuleService _moduleService = new ModuleService();

	internal TextBox SearchTextBox;

	internal TextBox TagsTextBox;

	internal ComboBox SortCombo;

	internal ComboBox OrderCombo;

	internal Button UpdateAllButton;

	internal ListView ModulesList;

	internal TextBlock TitleText;

	internal WebView2 DescriptionWebView;

	internal Button InstallButton;

	internal Button UninstallButton;

	internal Button UpdateButton;

	internal Border DevPanel;

	private bool _contentLoaded;

	public ModulesLibraryWindow(string apiBaseUrl = null, string userId = null)
	{
		InitializeComponent();
		_vm = new ModulesLibraryViewModel();
		base.DataContext = _vm;
		base.Loaded += async delegate
		{
			try
			{
				if (DescriptionWebView.CoreWebView2 == null)
				{
					await DescriptionWebView.EnsureCoreWebView2Async();
				}
			}
			catch
			{
			}
			await _vm.InitAsync();
			TrySetHtml(_vm.DescriptionHtml);
		};
		_vm.PropertyChanged += delegate(object? _, PropertyChangedEventArgs args)
		{
			if (string.Equals(args.PropertyName, "DescriptionHtml", StringComparison.Ordinal))
			{
				TrySetHtml(_vm.DescriptionHtml);
			}
		};
		base.Closed += delegate
		{
			try
			{
				Window window = Application.Current?.MainWindow;
				if (window != null)
				{
					if (!window.IsVisible)
					{
						window.Show();
					}
					window.Activate();
					window.Topmost = true;
					window.Topmost = false;
					window.Focus();
				}
			}
			catch
			{
			}
		};
	}

	private async Task UpdateDevPanelAsync()
	{
		try
		{
			if (_api.CurrentUser == null)
			{
				await _api.MeAsync();
			}
			DevPanel.Visibility = ((!(_api.CurrentUser?.Developer ?? false)) ? Visibility.Collapsed : Visibility.Visible);
		}
		catch
		{
			DevPanel.Visibility = Visibility.Collapsed;
		}
	}

	private async Task SearchAndBindAsync()
	{
		try
		{
			string sort = (SortCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
			string order = (OrderCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
			PagedModulesResponse pagedModulesResponse = await _api.SearchAsync(SearchTextBox.Text, TagsTextBox.Text, sort, order, 1, 50);
			ModulesList.ItemsSource = pagedModulesResponse.Items;
			UpdateAllButton.IsEnabled = ComputeAndSetUpdateIndicators(pagedModulesResponse.Items) > 0;
			if (pagedModulesResponse.Items.Count > 0)
			{
				ModulesList.SelectedIndex = 0;
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "Ошибка загрузки библиотеки: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async void OnSearchClick(object sender, RoutedEventArgs e)
	{
		await SearchAndBindAsync();
	}

	private void ModulesList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		_selected = ModulesList.SelectedItem as ModuleDto;
		if (_selected != null)
		{
			string localVersion = GetLocalVersion(_selected.Id);
			string text = _selected.Version ?? "1.0.0";
			string text2 = (string.IsNullOrWhiteSpace(text) ? _selected.Name : (_selected.Name + "  v" + text));
			text2 = text2 + " Автор: " + _selected.AuthorUsername;
			if (!string.IsNullOrWhiteSpace(localVersion))
			{
				string text3 = localVersion ?? string.Empty;
				string text4 = text ?? string.Empty;
				if (IsUpdateAvailable(text3, text4))
				{
					text2 = text2 + "  (локально v" + text3 + " — доступно v" + text4 + ")";
					UpdateButton.IsEnabled = true;
				}
				else
				{
					text2 = text2 + "  (локально v" + text3 + ")";
					UpdateButton.IsEnabled = false;
				}
			}
			else
			{
				UpdateButton.IsEnabled = false;
			}
			TitleText.Text = text2;
		}
		else
		{
			TitleText.Text = string.Empty;
			UpdateButton.IsEnabled = false;
		}
		string text5 = _selected?.Description ?? string.Empty;
		string html = (string.IsNullOrWhiteSpace(text5) ? "<i>Нет описания</i>" : Markdown.ToHtml(text5));
		TrySetHtml(html);
		UpdateActionButtons();
	}

	private int ComputeAndSetUpdateIndicators(IList<ModuleDto> items)
	{
		try
		{
			List<ModuleDefinition> source = _moduleService.LoadModules();
			int num = 0;
			foreach (ModuleDto m in items)
			{
				ModuleDefinition moduleDefinition = source.FirstOrDefault((ModuleDefinition x) => string.Equals(x.Id, m.Id.ToString(), StringComparison.OrdinalIgnoreCase));
				if (moduleDefinition != null && IsUpdateAvailable(moduleDefinition.Version ?? "1.0.0", m.Version ?? "1.0.0"))
				{
					num++;
				}
			}
			return num;
		}
		catch
		{
			return 0;
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

	private static bool IsUpdateAvailable(string local, string server)
	{
		if (TryParseVersion(local, out var ver) && TryParseVersion(server, out var ver2))
		{
			return ver2 > ver;
		}
		return !string.Equals(local, server, StringComparison.Ordinal);
	}

	private async void TrySetHtml(string html)
	{
		try
		{
			if (DescriptionWebView.CoreWebView2 == null)
			{
				await DescriptionWebView.EnsureCoreWebView2Async();
			}
			string value = "body{font-family:Segoe UI,Arial,sans-serif;padding:12px;background:#171A21;color:#C7D5E0;}\r\n                    h1,h2,h3,h4,h5,h6{color:#C7D5E0}\r\n                    a{color:#66C0F4}\r\n                    pre{background:#1B2838;padding:8px;border-radius:6px;overflow:auto;border:1px solid #2A475E}\r\n                    code{background:#1B2838;padding:2px 4px;border-radius:4px;border:1px solid #2A475E}\r\n                    blockquote{border-left:3px solid #2A475E;margin:8px 0;padding:4px 12px;color:#B8C6D1}\r\n                    table{border-collapse:collapse}\r\n                    th,td{border:1px solid #2A475E;padding:6px}\r\n                    ul,ol{padding-left:22px}";
			string htmlContent = $"<!DOCTYPE html><html><head><meta charset='utf-8'><style>{value}</style></head><body>{html}</body></html>";
			DescriptionWebView.NavigateToString(htmlContent);
		}
		catch
		{
		}
	}

	private void UpdateActionButtons()
	{
		try
		{
			if (_selected == null)
			{
				InstallButton.Visibility = Visibility.Collapsed;
				UninstallButton.Visibility = Visibility.Collapsed;
				UpdateButton.Visibility = Visibility.Collapsed;
				return;
			}
			string localVersion = GetLocalVersion(_selected.Id);
			bool flag = !string.IsNullOrWhiteSpace(localVersion);
			InstallButton.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			UninstallButton.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			string server = _selected.Version ?? "1.0.0";
			bool flag2 = flag && IsUpdateAvailable(localVersion, server);
			UpdateButton.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
			UpdateButton.IsEnabled = flag2;
		}
		catch
		{
		}
	}

	private async void OnUpdateSelectedClick(object sender, RoutedEventArgs e)
	{
		if (_selected == null)
		{
			return;
		}
		try
		{
			string localVersion = GetLocalVersion(_selected.Id);
			if (string.IsNullOrWhiteSpace(localVersion) || !IsUpdateAvailable(localVersion, _selected.Version ?? "1.0.0"))
			{
				MessageBox.Show(this, "Обновление не требуется", "Модули", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			InstallModuleLocally(_selected);
			RefreshOwnerModules();
			await SearchAndBindAsync();
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "Не удалось обновить модуль: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async void OnUpdateAllClick(object sender, RoutedEventArgs e)
	{
		try
		{
			List<ModuleDto> list = ModulesList.Items.Cast<object>().OfType<ModuleDto>().ToList();
			List<ModuleDefinition> source = _moduleService.LoadModules();
			int num = 0;
			foreach (ModuleDto m in list)
			{
				ModuleDefinition moduleDefinition = source.FirstOrDefault((ModuleDefinition x) => string.Equals(x.Id, m.Id.ToString(), StringComparison.OrdinalIgnoreCase));
				if (moduleDefinition != null && IsUpdateAvailable(moduleDefinition.Version ?? "1.0.0", m.Version ?? "1.0.0"))
				{
					InstallModuleLocally(m);
					num++;
				}
			}
			if (num > 0)
			{
				MessageBox.Show(this, $"Обновлено модулей: {num}", "Обновление модулей", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				RefreshOwnerModules();
				await SearchAndBindAsync();
			}
			else
			{
				MessageBox.Show(this, "Нет модулей, требующих обновления", "Обновление модулей", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "Ошибка при обновлении модулей: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async void OnInstallClick(object sender, RoutedEventArgs e)
	{
		if (_selected == null)
		{
			return;
		}
		try
		{
			await _api.UninstallAsync(_selected.Id, _userId);
			ModuleDto moduleDto = await _api.InstallAsync(_selected.Id, _userId);
			if (moduleDto != null)
			{
				_selected.InstallCount = moduleDto.InstallCount;
				ModulesList.Items.Refresh();
				InstallModuleLocally(moduleDto);
				RefreshOwnerModules();
				UpdateActionButtons();
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "Не удалось установить модуль: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async void OnUninstallClick(object sender, RoutedEventArgs e)
	{
		if (_selected == null)
		{
			return;
		}
		try
		{
			await _api.InstallAsync(_selected.Id, _userId);
			ModuleDto moduleDto = await _api.UninstallAsync(_selected.Id, _userId);
			if (moduleDto != null)
			{
				_selected.InstallCount = moduleDto.InstallCount;
				ModulesList.Items.Refresh();
				RemoveModuleLocally(_selected.Id);
				RefreshOwnerModules();
				UpdateActionButtons();
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "Не удалось удалить модуль: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void InstallModuleLocally(ModuleDto module)
	{
		try
		{
			string text = Path.Combine(AppContext.BaseDirectory, "Scripts");
			Directory.CreateDirectory(text);
			string text2 = module.Id.ToString() + ".lua";
			File.WriteAllText(Path.Combine(text, text2), module.Script ?? string.Empty);
			ModuleDefinition module2 = new ModuleDefinition
			{
				Id = module.Id.ToString(),
				Name = module.Name,
				Version = (string.IsNullOrWhiteSpace(module.Version) ? "1.0.0" : module.Version),
				Description = (module.Description ?? string.Empty),
				Script = text2,
				Inputs = (module.Inputs?.Select((InputDefinitionDto i) => new ModuleInput
				{
					Name = i.Name,
					Label = (string.IsNullOrWhiteSpace(i.Label) ? i.Name : i.Label),
					Type = (string.IsNullOrWhiteSpace(i.Type) ? "string" : i.Type),
					Default = (string.IsNullOrWhiteSpace(i.Default) ? null : i.Default),
					Required = i.Required
				}).ToList() ?? new List<ModuleInput>())
			};
			new ModuleService().AddOrUpdateModule(module2);
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message);
		}
	}

	private void RemoveModuleLocally(Guid id)
	{
		try
		{
			ModuleService moduleService = new ModuleService();
			List<ModuleDefinition> list = moduleService.LoadModules();
			ModuleDefinition moduleDefinition = list.FirstOrDefault((ModuleDefinition m) => string.Equals(m.Id, id.ToString(), StringComparison.OrdinalIgnoreCase));
			if (moduleDefinition == null)
			{
				return;
			}
			try
			{
				string baseDirectory = AppContext.BaseDirectory;
				string path = Path.Combine(baseDirectory, moduleDefinition.Script);
				string path2 = Path.Combine(baseDirectory, "Scripts", moduleDefinition.Script);
				if (File.Exists(path))
				{
					File.Delete(path);
				}
				else if (File.Exists(path2))
				{
					File.Delete(path2);
				}
			}
			catch
			{
			}
			list.Remove(moduleDefinition);
			moduleService.SaveModules(list);
		}
		catch
		{
		}
	}

	private void RefreshOwnerModules()
	{
		try
		{
			if (base.Owner is MainWindow mainWindow)
			{
				mainWindow.LoadModules();
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
			Uri resourceLocator = new Uri("/Pw.Hub;component/windows/moduleslibrarywindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			SearchTextBox = (TextBox)target;
			break;
		case 2:
			TagsTextBox = (TextBox)target;
			break;
		case 3:
			SortCombo = (ComboBox)target;
			break;
		case 4:
			OrderCombo = (ComboBox)target;
			break;
		case 5:
			UpdateAllButton = (Button)target;
			break;
		case 6:
			ModulesList = (ListView)target;
			break;
		case 7:
			TitleText = (TextBlock)target;
			break;
		case 8:
			DescriptionWebView = (WebView2)target;
			break;
		case 9:
			InstallButton = (Button)target;
			break;
		case 10:
			UninstallButton = (Button)target;
			break;
		case 11:
			UpdateButton = (Button)target;
			break;
		case 12:
			DevPanel = (Border)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

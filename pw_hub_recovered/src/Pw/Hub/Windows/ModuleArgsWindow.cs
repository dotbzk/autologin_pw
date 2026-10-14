using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Services;
using Pw.Hub.ViewModels;

namespace Pw.Hub.Windows;

public class ModuleArgsWindow : Window, IComponentConnector
{
	private class AccountSelectionRef
	{
		public Account Account { get; set; }

		public CheckBox Toggle { get; set; }
	}

	private class SquadGroupRefs
	{
		public Squad Squad { get; set; }

		public CheckBox HeaderCheck { get; set; }

		public TextBlock HeaderText { get; set; }

		public List<AccountSelectionRef> Items { get; set; } = new List<AccountSelectionRef>();
	}

	private class AccountsGroupedModel
	{
		public List<SquadGroupRefs> Groups { get; set; } = new List<SquadGroupRefs>();

		public IEnumerable<AccountSelectionRef> AllItems => Groups.SelectMany((SquadGroupRefs g) => g.Items);
	}

	private readonly ModuleDefinition _module;

	private readonly Dictionary<string, FrameworkElement> _inputs = new Dictionary<string, FrameworkElement>();

	internal StackPanel InputsPanel;

	private bool _contentLoaded;

	public ModuleArgsViewModel Vm { get; }

	public Dictionary<string, object> Values { get; } = new Dictionary<string, object>();

	public Dictionary<string, string> StringValues { get; } = new Dictionary<string, string>();

	public ModuleArgsWindow(ModuleDefinition module)
	{
		InitializeComponent();
		_module = module;
		Vm = new ModuleArgsViewModel
		{
			Module = module
		};
		Vm.RequestClose += OnRequestClose;
		base.DataContext = Vm;
		BuildUi();
		PrefillFromLastArgs();
	}

	public ModuleArgsWindow(IList<InputDefinitionDto> inputs, string title = "Launch Arguments")
	{
		InitializeComponent();
		_module = new ModuleDefinition
		{
			Name = title,
			Inputs = inputs.Select((InputDefinitionDto dto) => new ModuleInput
			{
				Name = (dto.Name ?? string.Empty),
				Label = (string.IsNullOrWhiteSpace(dto.Label) ? (dto.Name ?? string.Empty) : dto.Label),
				Type = (string.IsNullOrWhiteSpace(dto.Type) ? "string" : dto.Type),
				Default = dto.Default,
				Required = dto.Required,
				Options = (dto.Options?.ToList() ?? new List<string>())
			}).ToList()
		};
		Vm = new ModuleArgsViewModel
		{
			Module = _module
		};
		Vm.RequestClose += OnRequestClose;
		base.DataContext = Vm;
		BuildUi();
	}

	private void BuildUi()
	{
		InputsPanel.Children.Clear();
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Vertical,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		if (_module.Inputs.Count == 0)
		{
			TextBlock textBlock = new TextBlock
			{
				Text = "This module does not require launch arguments."
			};
			if (TryFindResource("ModernTextBlock") is Style style)
			{
				textBlock.Style = style;
			}
			textBlock.TextAlignment = TextAlignment.Center;
			stackPanel.Children.Add(textBlock);
		}
		foreach (ModuleInput input in _module.Inputs)
		{
			TextBlock textBlock2 = new TextBlock
			{
				Text = (string.IsNullOrWhiteSpace(input.Label) ? input.Name : input.Label)
			};
			if (TryFindResource("ModernTextBlock") is Style style2)
			{
				textBlock2.Style = style2;
			}
			stackPanel.Children.Add(textBlock2);
			ComboBox comboBox;
			FrameworkElement frameworkElement;
			switch ((input.Type ?? "string").ToLowerInvariant())
			{
			case "enum":
			case "перечисление":
				comboBox = new ComboBox();
				if (TryFindResource("ModernComboBox") is Style style12)
				{
					comboBox.Style = style12;
				}
				comboBox.ItemsSource = input.Options ?? new List<string>();
				if (!string.IsNullOrWhiteSpace(input.Default))
				{
					List<string> options = input.Options;
					if (options != null && options.Contains(input.Default))
					{
						comboBox.SelectedItem = input.Default;
						goto IL_0478;
					}
				}
				if ((input.Options?.Count ?? 0) > 0)
				{
					comboBox.SelectedIndex = 0;
				}
				goto IL_0478;
			case "bool":
			case "boolean":
			{
				bool result;
				CheckBox checkBox = new CheckBox
				{
					IsChecked = (bool.TryParse(input.Default, out result) && result)
				};
				if (TryFindResource("ModernCheckBox") is Style style4)
				{
					checkBox.Style = style4;
				}
				frameworkElement = checkBox;
				break;
			}
			case "double":
			case "number":
			case "int":
			{
				TextBox textBox2 = new TextBox
				{
					Text = (input.Default ?? string.Empty)
				};
				if (TryFindResource("ModernTextBox") is Style style13)
				{
					textBox2.Style = style13;
				}
				frameworkElement = textBox2;
				break;
			}
			case "пароль":
			case "password":
			{
				PasswordBox passwordBox = new PasswordBox();
				if (!string.IsNullOrEmpty(input.Default))
				{
					passwordBox.Password = input.Default;
				}
				if (TryFindResource("ModernPasswordBox") is Style style14)
				{
					passwordBox.Style = style14;
				}
				frameworkElement = passwordBox;
				break;
			}
			case "squad":
			case "отряд":
			{
				ComboBox comboBox2 = new ComboBox();
				if (TryFindResource("ModernComboBox") is Style style7)
				{
					comboBox2.Style = style7;
				}
				try
				{
					using AppDbContext appDbContext2 = new AppDbContext();
					List<Squad> itemsSource2 = (from s in appDbContext2.Squads.Include((Squad s) => s.Accounts).ThenInclude((Account a) => a.Servers).ThenInclude((AccountServer sv) => sv.Characters)
						orderby s.OrderIndex, s.Name
						select s).ToList();
					comboBox2.ItemsSource = itemsSource2;
					comboBox2.DisplayMemberPath = "Name";
				}
				catch
				{
				}
				frameworkElement = comboBox2;
				break;
			}
			case "squads":
			case "отряды":
			{
				StackPanel stackPanel2 = new StackPanel
				{
					Orientation = Orientation.Vertical
				};
				StackPanel stackPanel3 = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Margin = new Thickness(0.0, 0.0, 0.0, 6.0),
					HorizontalAlignment = HorizontalAlignment.Right
				};
				Button button = new Button
				{
					Content = "Select All",
					Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
				};
				Button button2 = new Button
				{
					Content = "Clear All"
				};
				if (TryFindResource("ModernButton") is Style style5)
				{
					button.Style = style5;
					button2.Style = style5;
				}
				ListBox lbSquads = new ListBox
				{
					SelectionMode = SelectionMode.Extended,
					Height = 160.0
				};
				if (TryFindResource("ModernListBox") is Style style6)
				{
					lbSquads.Style = style6;
				}
				try
				{
					using AppDbContext appDbContext = new AppDbContext();
					List<Squad> itemsSource = (from s in appDbContext.Squads.Include((Squad s) => s.Accounts).ThenInclude((Account a) => a.Servers).ThenInclude((AccountServer sv) => sv.Characters)
						orderby s.OrderIndex, s.Name
						select s).ToList();
					lbSquads.ItemsSource = itemsSource;
					lbSquads.DisplayMemberPath = "Name";
				}
				catch
				{
				}
				button.Click += delegate
				{
					try
					{
						lbSquads.SelectAll();
					}
					catch
					{
					}
				};
				button2.Click += delegate
				{
					try
					{
						lbSquads.UnselectAll();
					}
					catch
					{
					}
				};
				stackPanel3.Children.Add(button);
				stackPanel3.Children.Add(button2);
				stackPanel2.Children.Add(stackPanel3);
				stackPanel2.Children.Add(lbSquads);
				frameworkElement = stackPanel2;
				frameworkElement.Tag = input;
				lbSquads.Tag = input;
				_inputs[input.Name] = lbSquads;
				stackPanel.Children.Add(frameworkElement);
				continue;
			}
			case "account":
			case "аккаунт":
			{
				ComboBox comboBox3 = new ComboBox();
				if (TryFindResource("ModernComboBox") is Style style15)
				{
					comboBox3.Style = style15;
				}
				try
				{
					using AppDbContext appDbContext4 = new AppDbContext();
					List<Account> itemsSource3 = (from a in appDbContext4.Accounts.Include((Account a) => a.Servers).ThenInclude((AccountServer s) => s.Characters).Include((Account a) => a.Squad)
						orderby a.OrderIndex, a.Name
						select a).ToList();
					comboBox3.ItemsSource = itemsSource3;
					comboBox3.DisplayMemberPath = "Name";
				}
				catch
				{
				}
				frameworkElement = comboBox3;
				break;
			}
			case "accounts":
			case "аккаунты":
			{
				StackPanel stackPanel4 = new StackPanel
				{
					Orientation = Orientation.Vertical
				};
				StackPanel stackPanel5 = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Margin = new Thickness(0.0, 0.0, 0.0, 6.0),
					HorizontalAlignment = HorizontalAlignment.Right
				};
				Button button3 = new Button
				{
					Content = "Select All",
					Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
				};
				Button button4 = new Button
				{
					Content = "Clear All"
				};
				if (TryFindResource("ModernButton") is Style style8)
				{
					button3.Style = style8;
					button4.Style = style8;
				}
				stackPanel5.Children.Add(button3);
				stackPanel5.Children.Add(button4);
				stackPanel4.Children.Add(stackPanel5);
				ScrollViewer scrollViewer = new ScrollViewer
				{
					VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
					Height = 260.0
				};
				StackPanel stackPanel6 = (StackPanel)(scrollViewer.Content = new StackPanel
				{
					Orientation = Orientation.Vertical
				});
				stackPanel4.Children.Add(scrollViewer);
				AccountsGroupedModel model = new AccountsGroupedModel();
				try
				{
					using AppDbContext appDbContext3 = new AppDbContext();
					foreach (Squad item2 in (from s in appDbContext3.Squads.Include((Squad s) => s.Accounts)
						orderby s.OrderIndex, s.Name
						select s).ToList())
					{
						SquadGroupRefs g = new SquadGroupRefs
						{
							Squad = item2
						};
						DockPanel dockPanel = new DockPanel
						{
							Margin = new Thickness(0.0, 4.0, 0.0, 4.0)
						};
						CheckBox checkBox2 = new CheckBox
						{
							IsThreeState = true,
							VerticalAlignment = VerticalAlignment.Center
						};
						if (TryFindResource("ModernCheckBox") is Style style9)
						{
							checkBox2.Style = style9;
						}
						TextBlock textBlock3 = new TextBlock
						{
							Text = item2.Name,
							Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
						};
						if (TryFindResource("ModernTextBlock") is Style style10)
						{
							textBlock3.Style = style10;
						}
						textBlock3.MouseLeftButtonUp += delegate
						{
							try
							{
								bool value = !g.Items.Any((AccountSelectionRef i) => i.Toggle.IsChecked == true);
								foreach (AccountSelectionRef item3 in g.Items)
								{
									item3.Toggle.IsChecked = value;
								}
								UpdateGroupHeader(g);
							}
							catch
							{
							}
						};
						DockPanel.SetDock(checkBox2, Dock.Left);
						dockPanel.Children.Add(checkBox2);
						dockPanel.Children.Add(textBlock3);
						g.HeaderCheck = checkBox2;
						g.HeaderText = textBlock3;
						List<Account> list = (from a in item2.Accounts ?? new ObservableCollection<Account>()
							orderby a.OrderIndex, a.Name
							select a).ToList();
						StackPanel stackPanel7 = new StackPanel
						{
							Orientation = Orientation.Vertical,
							Margin = new Thickness(24.0, 2.0, 0.0, 8.0)
						};
						foreach (Account item4 in list)
						{
							CheckBox checkBox3 = new CheckBox
							{
								Content = item4.Name,
								Margin = new Thickness(0.0, 2.0, 0.0, 2.0),
								HorizontalAlignment = HorizontalAlignment.Stretch,
								Padding = new Thickness(10.0, 6.0, 10.0, 6.0)
							};
							if (TryFindResource("ModernCheckBox") is Style style11)
							{
								checkBox3.Style = style11;
							}
							AccountSelectionRef item = new AccountSelectionRef
							{
								Account = item4,
								Toggle = checkBox3
							};
							checkBox3.Checked += delegate
							{
								try
								{
									UpdateGroupHeader(g);
								}
								catch
								{
								}
							};
							checkBox3.Unchecked += delegate
							{
								try
								{
									UpdateGroupHeader(g);
								}
								catch
								{
								}
							};
							stackPanel7.Children.Add(checkBox3);
							g.Items.Add(item);
						}
						checkBox2.Click += delegate
						{
							try
							{
								bool value = !g.Items.Any((AccountSelectionRef i) => i.Toggle.IsChecked == true);
								foreach (AccountSelectionRef item5 in g.Items)
								{
									item5.Toggle.IsChecked = value;
								}
								UpdateGroupHeader(g);
							}
							catch
							{
							}
						};
						StackPanel stackPanel8 = new StackPanel
						{
							Orientation = Orientation.Vertical
						};
						stackPanel8.Children.Add(dockPanel);
						stackPanel8.Children.Add(stackPanel7);
						stackPanel6.Children.Add(stackPanel8);
						model.Groups.Add(g);
						UpdateGroupHeader(g);
					}
				}
				catch
				{
				}
				button3.Click += delegate
				{
					try
					{
						foreach (AccountSelectionRef allItem in model.AllItems)
						{
							allItem.Toggle.IsChecked = true;
						}
						foreach (SquadGroupRefs group in model.Groups)
						{
							UpdateGroupHeader(group);
						}
					}
					catch
					{
					}
				};
				button4.Click += delegate
				{
					try
					{
						foreach (AccountSelectionRef allItem2 in model.AllItems)
						{
							allItem2.Toggle.IsChecked = false;
						}
						foreach (SquadGroupRefs group2 in model.Groups)
						{
							UpdateGroupHeader(group2);
						}
					}
					catch
					{
					}
				};
				stackPanel4.Tag = input;
				stackPanel4.DataContext = model;
				frameworkElement = stackPanel4;
				_inputs[input.Name] = stackPanel4;
				stackPanel.Children.Add(stackPanel4);
				continue;
			}
			default:
				{
					TextBox textBox = new TextBox
					{
						Text = (input.Default ?? string.Empty)
					};
					if (TryFindResource("ModernTextBox") is Style style3)
					{
						textBox.Style = style3;
					}
					frameworkElement = textBox;
					break;
				}
				IL_0478:
				frameworkElement = comboBox;
				break;
			}
			frameworkElement.Tag = input;
			_inputs[input.Name] = frameworkElement;
			stackPanel.Children.Add(frameworkElement);
		}
		InputsPanel.Children.Add(stackPanel);
	}

	private void UpdateGroupHeader(SquadGroupRefs g)
	{
		try
		{
			int count = g.Items.Count;
			int num = g.Items.Count((AccountSelectionRef i) => i.Toggle.IsChecked == true);
			bool flag = num > 0;
			bool flag2 = num == count && count > 0;
			g.HeaderCheck.IsChecked = (flag2 ? new bool?(true) : (flag ? ((bool?)null) : new bool?(false)));
			if (g.HeaderText != null)
			{
				string text = g.Squad?.Name ?? string.Empty;
				g.HeaderText.Text = ((count > 0) ? $"{text} — {num} / {count}" : text);
			}
		}
		catch
		{
		}
	}

	private void PrefillFromLastArgs()
	{
		try
		{
			Dictionary<string, string> dictionary = _module.LastArgs ?? new Dictionary<string, string>();
			foreach (KeyValuePair<string, FrameworkElement> input in _inputs)
			{
				string key = input.Key;
				FrameworkElement value = input.Value;
				ModuleInput moduleInput = (ModuleInput)value.Tag;
				if (!dictionary.TryGetValue(key, out var value2))
				{
					continue;
				}
				string text = (moduleInput.Type ?? "string").ToLowerInvariant();
				if (value is CheckBox checkBox)
				{
					if (bool.TryParse(value2, out var result))
					{
						checkBox.IsChecked = result;
					}
				}
				else if (value is ComboBox comboBox && (text == "enum" || text == "перечисление"))
				{
					try
					{
						if (string.IsNullOrWhiteSpace(value2))
						{
							continue;
						}
						IEnumerable itemsSource = comboBox.ItemsSource;
						if (itemsSource == null)
						{
							continue;
						}
						foreach (object item in itemsSource)
						{
							if (item is string a && string.Equals(a, value2, StringComparison.Ordinal))
							{
								comboBox.SelectedItem = item;
								break;
							}
						}
					}
					catch
					{
					}
				}
				else if (value is TextBox textBox)
				{
					textBox.Text = value2 ?? string.Empty;
				}
				else if (value is PasswordBox passwordBox)
				{
					passwordBox.Password = value2 ?? string.Empty;
				}
				else if (value is ComboBox comboBox2 && (text == "отряд" || text == "squad"))
				{
					try
					{
						string text2 = value2;
						if (string.IsNullOrWhiteSpace(text2))
						{
							continue;
						}
						IEnumerable itemsSource2 = comboBox2.ItemsSource;
						if (itemsSource2 == null)
						{
							continue;
						}
						foreach (object item2 in itemsSource2)
						{
							if (item2 is Squad squad && string.Equals(squad.Id, text2, StringComparison.OrdinalIgnoreCase))
							{
								comboBox2.SelectedItem = item2;
								break;
							}
						}
					}
					catch
					{
					}
				}
				else if (value is ListBox listBox && (text == "отряды" || text == "squads"))
				{
					try
					{
						HashSet<string> hashSet = new HashSet<string>((value2 ?? string.Empty).Split(new char[3] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
						foreach (object item3 in (IEnumerable)listBox.Items)
						{
							if (item3 is Squad squad2 && hashSet.Contains(squad2.Id))
							{
								listBox.SelectedItems.Add(item3);
							}
						}
					}
					catch
					{
					}
				}
				else if (value is ComboBox comboBox3 && (text == "аккаунт" || text == "account"))
				{
					try
					{
						string text3 = value2;
						if (string.IsNullOrWhiteSpace(text3))
						{
							continue;
						}
						IEnumerable itemsSource3 = comboBox3.ItemsSource;
						if (itemsSource3 == null)
						{
							continue;
						}
						foreach (object item4 in itemsSource3)
						{
							if (item4 is Account account && string.Equals(account.Id, text3, StringComparison.OrdinalIgnoreCase))
							{
								comboBox3.SelectedItem = item4;
								break;
							}
						}
					}
					catch
					{
					}
				}
				else
				{
					if (!(text == "аккаунты") && !(text == "accounts"))
					{
						continue;
					}
					try
					{
						HashSet<string> hashSet2 = new HashSet<string>((value2 ?? string.Empty).Split(new char[3] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
						if (value is ListBox listBox2)
						{
							foreach (object item5 in (IEnumerable)listBox2.Items)
							{
								if (item5 is Account account2 && hashSet2.Contains(account2.Id))
								{
									listBox2.SelectedItems.Add(item5);
								}
							}
						}
						else
						{
							if (value == null)
							{
								continue;
							}
							FrameworkElement frameworkElement = value;
							if (!(frameworkElement.DataContext is AccountsGroupedModel accountsGroupedModel))
							{
								continue;
							}
							foreach (SquadGroupRefs group in accountsGroupedModel.Groups)
							{
								foreach (AccountSelectionRef item6 in group.Items)
								{
									if (item6.Account != null && !string.IsNullOrEmpty(item6.Account.Id))
									{
										item6.Toggle.IsChecked = hashSet2.Contains(item6.Account.Id);
									}
								}
								UpdateGroupHeader(group);
							}
							continue;
						}
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
	}

	private void Ok_Click(object sender, RoutedEventArgs e)
	{
		Values.Clear();
		StringValues.Clear();
		foreach (KeyValuePair<string, FrameworkElement> input in _inputs)
		{
			string key = input.Key;
			FrameworkElement value = input.Value;
			ModuleInput moduleInput = (ModuleInput)value.Tag;
			object obj = null;
			string text = string.Empty;
			string text2 = (moduleInput.Type ?? "string").ToLowerInvariant();
			if (value is CheckBox { IsChecked: var isChecked })
			{
				bool valueOrDefault = isChecked == true;
				obj = valueOrDefault;
				text = (valueOrDefault ? "true" : "false");
			}
			else if (value is ComboBox comboBox && (text2 == "enum" || text2 == "перечисление"))
			{
				object obj2 = comboBox.SelectedItem as string;
				obj = obj2 ?? string.Empty;
				if (obj2 == null)
				{
					obj2 = string.Empty;
				}
				text = (string)obj2;
			}
			else if (value is TextBox textBox)
			{
				string text3 = textBox.Text ?? string.Empty;
				bool flag;
				switch (text2)
				{
				case "number":
				case "int":
				case "double":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (flag)
				{
					if (double.TryParse(text3, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
					{
						obj = result;
						text = result.ToString(CultureInfo.InvariantCulture);
					}
					else
					{
						obj = 0.0;
						text = "0";
					}
				}
				else
				{
					obj = text3;
					text = text3;
				}
			}
			else if (value is PasswordBox passwordBox)
			{
				string obj3 = passwordBox.Password ?? string.Empty;
				obj = obj3;
				text = obj3;
			}
			else if (value is ComboBox comboBox2 && (text2 == "отряд" || text2 == "squad"))
			{
				text = ((Squad)(obj = comboBox2.SelectedItem as Squad))?.Id ?? string.Empty;
			}
			else if (value is ListBox listBox && (text2 == "отряды" || text2 == "squads"))
			{
				List<Squad> list = listBox.SelectedItems.Cast<object>().OfType<Squad>().ToList();
				obj = list;
				text = string.Join(",", list.Select((Squad s) => s.Id ?? string.Empty));
			}
			else if (value is ComboBox comboBox3 && (text2 == "аккаунт" || text2 == "account"))
			{
				text = ((Account)(obj = comboBox3.SelectedItem as Account))?.Id ?? string.Empty;
			}
			else if (text2 == "аккаунты" || text2 == "accounts")
			{
				if (value is ListBox listBox2)
				{
					List<Account> list2 = listBox2.SelectedItems.Cast<object>().OfType<Account>().ToList();
					obj = list2;
					text = string.Join(",", list2.Select((Account a) => a.Id ?? string.Empty));
				}
				else if (value != null)
				{
					FrameworkElement frameworkElement = value;
					if (frameworkElement.DataContext is AccountsGroupedModel accountsGroupedModel)
					{
						List<Account> list3 = (from i in accountsGroupedModel.AllItems
							where i.Toggle.IsChecked == true
							select i.Account into a
							where a != null
							select a).ToList();
						obj = list3;
						text = string.Join(",", list3.Select((Account a) => a.Id ?? string.Empty));
					}
				}
			}
			if (moduleInput.Required && (obj == null || (obj is string value2 && string.IsNullOrWhiteSpace(value2)) || obj is ICollection { Count: 0 }))
			{
				MessageBox.Show("Complete the field: " + (moduleInput.Label ?? moduleInput.Name), "Module Parameters", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			Values[key] = obj;
			StringValues[key] = text ?? string.Empty;
		}
		Vm.ConfirmWithValues(Values, StringValues);
	}

	private void OnRequestClose(bool? dialogResult)
	{
		try
		{
			if (Values.Count == 0 && Vm.Values.Count > 0)
			{
				foreach (KeyValuePair<string, object> value in Vm.Values)
				{
					Values[value.Key] = value.Value;
				}
			}
			if (StringValues.Count == 0 && Vm.StringValues.Count > 0)
			{
				foreach (KeyValuePair<string, string> stringValue in Vm.StringValues)
				{
					StringValues[stringValue.Key] = stringValue.Value;
				}
			}
			try
			{
				base.DialogResult = dialogResult;
			}
			catch
			{
			}
		}
		finally
		{
			Close();
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/Pw.Hub;component/windows/moduleargswindow.xaml", UriKind.Relative);
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
			InputsPanel = (StackPanel)target;
			break;
		case 2:
			((Button)target).Click += Ok_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

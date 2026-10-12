using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Markdig;
using Microsoft.Web.WebView2.Wpf;
using Pw.Hub.Models;
using Pw.Hub.Services;
using Pw.Hub.Tools;
using Pw.Hub.ViewModels;

namespace Pw.Hub.Windows;

public class ModulesApiEditorWindow : Window, IComponentConnector
{
	private class InputItem : INotifyPropertyChanged
	{
		private string _name = string.Empty;

		private string _label = string.Empty;

		private string _type = "string";

		private string _default;

		private bool _required;

		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				SetField(ref _name, value, "Name");
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
				SetField(ref _label, value, "Label");
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
				SetField(ref _type, value, "Type");
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
				SetField(ref _default, value, "Default");
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
				SetField(ref _required, value, "Required");
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
			{
				return false;
			}
			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}
	}

	private class AIConfig
	{
		public string OllamaApiKey { get; set; }
	}

	private readonly ModuleDto _existing;

	private CancellationTokenSource _previewCts;

	private string _script = string.Empty;

	private readonly ModulesApiEditorViewModel _vm = new ModulesApiEditorViewModel();

	internal ScrollViewer MainScrollViewer;

	internal TextBox NameText;

	internal TextBox VersionText;

	internal CheckBox ShowPreviewCheck;

	internal Button GenerateDescriptionBtn;

	internal TextBox DescriptionEditor;

	internal Border PreviewContainer;

	internal Border PreviewHeader;

	internal WebView2 PreviewWebView;

	internal Button OpenLuaEditorButton;

	internal Button AddInputButton;

	internal Button RemoveInputButton;

	internal DataGrid InputsGrid;

	private bool _contentLoaded;

	public bool IsSaved { get; private set; }

	public ModulesApiEditorWindow(ModuleDto existing = null)
	{
		InitializeComponent();
		_existing = existing;
		base.DataContext = _vm;
		base.Loaded += async delegate
		{
			await InitPreviewAsync();
			await Task.Delay(200);
			UpdateWebViewClipping();
		};
		if (existing != null)
		{
			NameText.Text = existing.Name;
			VersionText.Text = (string.IsNullOrWhiteSpace(existing.Version) ? "1.0.0" : existing.Version);
			DescriptionEditor.Text = existing.Description ?? string.Empty;
			_script = existing.Script ?? string.Empty;
			_vm.Name = NameText.Text;
			_vm.Version = VersionText.Text;
			_vm.Description = DescriptionEditor.Text;
			_vm.Script = _script;
			_vm.Inputs.Clear();
			InputDefinitionDto[] array = existing.Inputs ?? Array.Empty<InputDefinitionDto>();
			foreach (InputDefinitionDto inputDefinitionDto in array)
			{
				_vm.Inputs.Add(new ModulesApiEditorViewModel.InputItem
				{
					Name = (inputDefinitionDto.Name ?? string.Empty),
					Label = (string.IsNullOrWhiteSpace(inputDefinitionDto.Label) ? (inputDefinitionDto.Name ?? string.Empty) : inputDefinitionDto.Label),
					Type = (string.IsNullOrWhiteSpace(inputDefinitionDto.Type) ? "string" : inputDefinitionDto.Type),
					Default = inputDefinitionDto.Default,
					Required = inputDefinitionDto.Required,
					OptionsCsv = ((inputDefinitionDto.Options == null || inputDefinitionDto.Options.Length == 0) ? string.Empty : string.Join(", ", inputDefinitionDto.Options.Where((string s) => !string.IsNullOrWhiteSpace(s))))
				});
			}
			base.Title = "Редактирование: " + existing.Name;
		}
		else
		{
			_script = string.Empty;
			_vm.Name = string.Empty;
			_vm.Version = "1.0.0";
			_vm.Description = string.Empty;
			_vm.Script = _script;
		}
		_vm.RequestClose += delegate(bool? result)
		{
			if (result == true)
			{
				IsSaved = true;
			}
			try
			{
				base.DialogResult = result;
			}
			catch
			{
			}
			Close();
		};
	}

	private async Task InitPreviewAsync()
	{
		try
		{
			if (PreviewWebView.CoreWebView2 == null)
			{
				await PreviewWebView.EnsureCoreWebView2Async();
			}
			PreviewWebView.SizeChanged += delegate
			{
				UpdateWebViewClipping();
			};
			PreviewContainer.SizeChanged += delegate
			{
				UpdateWebViewClipping();
			};
			base.SizeChanged += delegate
			{
				UpdateWebViewClipping();
			};
		}
		catch
		{
		}
		await UpdatePreviewAsync();
		UpdatePreviewVisibility();
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			UpdateWebViewClipping();
		}, DispatcherPriority.Loaded);
	}

	public CreateOrUpdateModule GetRequest()
	{
		return _vm.BuildRequest();
	}

	private void OnSaveClick(object sender, RoutedEventArgs e)
	{
		try
		{
			InputsGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
			InputsGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
		}
		catch
		{
		}
		try
		{
			_script = _vm.Script ?? string.Empty;
		}
		catch
		{
		}
		if (string.IsNullOrWhiteSpace(_vm.Name) || string.IsNullOrWhiteSpace(_vm.Script))
		{
			MessageBox.Show(this, "Имя и скрипт обязательны");
			return;
		}
		IsSaved = true;
		try
		{
			_vm.GetType().GetProperty("IsSaved")?.SetValue(_vm, true);
		}
		catch
		{
		}
		try
		{
			base.DialogResult = true;
		}
		catch
		{
		}
		try
		{
			Close();
		}
		catch
		{
		}
	}

	private void AddInput_Click(object sender, RoutedEventArgs e)
	{
		_vm.Inputs.Add(new ModulesApiEditorViewModel.InputItem
		{
			Name = "param",
			Label = "Параметр",
			Type = "string",
			Default = string.Empty,
			Required = false
		});
	}

	private async void OpenLuaEditor_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (!(Application.Current.MainWindow is MainWindow mainWindow))
			{
				return;
			}
			LuaScriptRunner runner = mainWindow.AccountPage?.LuaRunner;
			if (runner == null)
			{
				MessageBox.Show(this, "Lua Runner недоступен", "Lua", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			await EnsureFullModuleLoadedAsync();
			string code = ((!string.IsNullOrWhiteSpace(_vm.Script)) ? _vm.Script : (_script ?? string.Empty));
			LuaEditorWindow editor = new LuaEditorWindow(runner)
			{
				Owner = Application.Current.MainWindow
			};
			editor.SetCode(code);
			List<InputDefinitionDto> apiInputs = _vm.Inputs.Select((ModulesApiEditorViewModel.InputItem i) => new InputDefinitionDto
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
			}).ToList();
			editor.ApiInputs = apiInputs;
			editor.Closed += delegate
			{
				try
				{
					_script = editor.GetCode();
					_vm.Script = _script;
					try
					{
						Activate();
					}
					catch
					{
					}
				}
				catch
				{
				}
			};
			editor.Show();
		}
		catch
		{
		}
	}

	private async Task EnsureFullModuleLoadedAsync()
	{
		try
		{
			if (_existing == null)
			{
				return;
			}
			bool needScript = string.IsNullOrWhiteSpace(_script);
			bool needInputs = _vm.Inputs == null || _vm.Inputs.Count == 0;
			if (!needScript && !needInputs)
			{
				return;
			}
			ModuleDto moduleDto = await new ModulesApiClient().GetModuleAsync(_existing.Id);
			if (moduleDto != null)
			{
				if (needScript)
				{
					_script = moduleDto.Script ?? string.Empty;
					_vm.Script = _script;
				}
				if (needInputs)
				{
					_vm.Inputs.Clear();
					InputDefinitionDto[] array = moduleDto.Inputs ?? Array.Empty<InputDefinitionDto>();
					foreach (InputDefinitionDto inputDefinitionDto in array)
					{
						_vm.Inputs.Add(new ModulesApiEditorViewModel.InputItem
						{
							Name = (inputDefinitionDto.Name ?? string.Empty),
							Label = (string.IsNullOrWhiteSpace(inputDefinitionDto.Label) ? (inputDefinitionDto.Name ?? string.Empty) : inputDefinitionDto.Label),
							Type = (string.IsNullOrWhiteSpace(inputDefinitionDto.Type) ? "string" : inputDefinitionDto.Type),
							Default = inputDefinitionDto.Default,
							Required = inputDefinitionDto.Required,
							OptionsCsv = ((inputDefinitionDto.Options == null || inputDefinitionDto.Options.Length == 0) ? string.Empty : string.Join(", ", inputDefinitionDto.Options.Where((string s) => !string.IsNullOrWhiteSpace(s))))
						});
					}
				}
			}
			if (!needScript || !string.IsNullOrWhiteSpace(_script))
			{
				return;
			}
			try
			{
				ModuleDefinition moduleDefinition = new ModuleService().LoadModules()?.FirstOrDefault((ModuleDefinition x) => string.Equals(x.Id, _existing.Id.ToString(), StringComparison.OrdinalIgnoreCase));
				if (moduleDefinition != null)
				{
					string baseDirectory = AppContext.BaseDirectory;
					string path = Path.Combine(baseDirectory, moduleDefinition.Script ?? string.Empty);
					string path2 = Path.Combine(baseDirectory, "Scripts", moduleDefinition.Script ?? string.Empty);
					string text = null;
					if (File.Exists(path))
					{
						text = File.ReadAllText(path);
					}
					else if (File.Exists(path2))
					{
						text = File.ReadAllText(path2);
					}
					if (!string.IsNullOrEmpty(text))
					{
						_script = text;
						_vm.Script = _script;
					}
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

	private void RemoveInput_Click(object sender, RoutedEventArgs e)
	{
		if (InputsGrid.SelectedItem is ModulesApiEditorViewModel.InputItem item)
		{
			_vm.Inputs.Remove(item);
		}
	}

	private void DescriptionEditor_OnTextChanged(object sender, TextChangedEventArgs e)
	{
		DebouncePreview();
	}

	private void ShowPreviewCheck_OnChanged(object sender, RoutedEventArgs e)
	{
		UpdatePreviewVisibility();
		if (ShowPreviewCheck.IsChecked == true)
		{
			DebouncePreview();
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				UpdateWebViewClipping();
			}, DispatcherPriority.Loaded);
		}
	}

	private void UpdatePreviewVisibility()
	{
		try
		{
			if (PreviewContainer == null || ShowPreviewCheck == null)
			{
				base.Dispatcher.BeginInvoke(new Action(UpdatePreviewVisibility));
				return;
			}
			bool valueOrDefault = ShowPreviewCheck.IsChecked == true;
			PreviewContainer.Visibility = ((!valueOrDefault) ? Visibility.Collapsed : Visibility.Visible);
		}
		catch
		{
		}
	}

	private void MainScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		UpdateWebViewClipping();
	}

	private void UpdateWebViewClipping()
	{
		try
		{
			if (PreviewWebView?.CoreWebView2 != null && MainScrollViewer != null && PreviewContainer != null && PreviewHeader != null)
			{
				Point point = PreviewContainer.TransformToAncestor(MainScrollViewer).Transform(new Point(0.0, 0.0));
				double actualHeight = MainScrollViewer.ActualHeight;
				double actualHeight2 = PreviewContainer.ActualHeight;
				double actualHeight3 = PreviewHeader.ActualHeight;
				double num = Math.Max(0.0, 0.0 - point.Y);
				double top = Math.Max(0.0, num - actualHeight3);
				double num2 = point.Y + actualHeight2;
				double num3 = Math.Max(0.0, num2 - actualHeight);
				if (num > 0.0 || num3 > 0.0)
				{
					double height = Math.Max(0.0, actualHeight2 - num - num3);
					RectangleGeometry clip = new RectangleGeometry(new Rect(0.0, num, PreviewContainer.ActualWidth, height));
					PreviewContainer.Clip = clip;
					PreviewWebView.Margin = new Thickness(0.0, top, 0.0, num3);
				}
				else
				{
					PreviewContainer.Clip = null;
					PreviewWebView.Margin = new Thickness(0.0);
				}
			}
		}
		catch
		{
		}
	}

	private void DebouncePreview()
	{
		_previewCts?.Cancel();
		_previewCts = new CancellationTokenSource();
		CancellationToken token = _previewCts.Token;
		Task.Run(async delegate
		{
			_ = 1;
			try
			{
				await Task.Delay(300, token);
				if (!token.IsCancellationRequested)
				{
					await base.Dispatcher.InvokeAsync((Func<Task>)async delegate
					{
						await UpdatePreviewAsync();
					});
				}
			}
			catch
			{
			}
		});
	}

	private async Task UpdatePreviewAsync()
	{
		_ = 1;
		try
		{
			string text = DescriptionEditor.Text ?? string.Empty;
			string html = (string.IsNullOrWhiteSpace(text) ? "<i>Нет описания</i>" : Markdown.ToHtml(text));
			if (PreviewWebView.CoreWebView2 == null)
			{
				await PreviewWebView.EnsureCoreWebView2Async();
			}
			string value = "html{height:100%;margin:0;padding:0;overflow:auto;}\r\n                    body{margin:0;padding:8px 12px 12px 12px;font-family:Segoe UI,Arial,sans-serif;background:#171A21;color:#C7D5E0;box-sizing:border-box;}\r\n                    *{box-sizing:border-box;max-width:100%;}\r\n                    h1,h2,h3,h4,h5,h6{color:#C7D5E0;margin:0.5em 0;}\r\n                    a{color:#66C0F4}\r\n                    pre{background:#1B2838;padding:8px;border-radius:6px;overflow-x:auto;white-space:pre-wrap;word-wrap:break-word;border:1px solid #2A475E;margin:8px 0;}\r\n                    code{background:#1B2838;padding:2px 4px;border-radius:4px;border:1px solid #2A475E;word-break:break-word;}\r\n                    blockquote{border-left:3px solid #2A475E;margin:8px 0;padding:4px 12px;color:#B8C6D1}\r\n                    table{border-collapse:collapse;width:100%;margin:8px 0;}\r\n                    th,td{border:1px solid #2A475E;padding:6px;text-align:left;}\r\n                    ul,ol{padding-left:22px;margin:8px 0;}\r\n                    p{margin:0.5em 0;}";
			string htmlContent = $"<!DOCTYPE html><html><head><meta charset='utf-8'><style>{value}</style></head><body>{html}</body></html>";
			PreviewWebView.NavigateToString(htmlContent);
			await Task.Delay(100);
			UpdateWebViewClipping();
		}
		catch
		{
		}
	}

	private void Bold_Click(object sender, RoutedEventArgs e)
	{
		WrapSelection("**", "**");
	}

	private void Italic_Click(object sender, RoutedEventArgs e)
	{
		WrapSelection("*", "*");
	}

	private void Header_Click(object sender, RoutedEventArgs e)
	{
		PrefixSelection("# ");
	}

	private void List_Click(object sender, RoutedEventArgs e)
	{
		PrefixEachLine("- ");
	}

	private void Code_Click(object sender, RoutedEventArgs e)
	{
		WrapSelection("````n", "\n```");
	}

	private void WrapSelection(string left, string right)
	{
		try
		{
			TextBox descriptionEditor = DescriptionEditor;
			string text = descriptionEditor.SelectedText;
			if (string.IsNullOrEmpty(text))
			{
				text = "текст";
			}
			string selectedText = left + text + right;
			int selectionStart = descriptionEditor.SelectionStart;
			descriptionEditor.SelectedText = selectedText;
			descriptionEditor.SelectionStart = selectionStart + left.Length;
			descriptionEditor.SelectionLength = text.Length;
		}
		catch
		{
		}
	}

	private void PrefixSelection(string prefix)
	{
		try
		{
			TextBox descriptionEditor = DescriptionEditor;
			int start = descriptionEditor.Text.LastIndexOf('\n', Math.Max(0, descriptionEditor.SelectionStart - 1)) + 1;
			descriptionEditor.Select(start, 0);
			descriptionEditor.SelectedText = prefix;
		}
		catch
		{
		}
	}

	private void PrefixEachLine(string prefix)
	{
		try
		{
			TextBox descriptionEditor = DescriptionEditor;
			int selectionStart = descriptionEditor.SelectionStart;
			int selectionLength = descriptionEditor.SelectionLength;
			string text = descriptionEditor.Text;
			string text2 = ((selectionLength > 0) ? text.Substring(selectionStart, selectionLength) : string.Empty);
			if (string.IsNullOrEmpty(text2))
			{
				PrefixSelection(prefix);
				return;
			}
			string[] source = text2.Replace("\r", string.Empty).Split('\n');
			string selectedText = string.Join("\n", source.Select((string l) => (!string.IsNullOrWhiteSpace(l)) ? (prefix + l) : l));
			descriptionEditor.SelectedText = selectedText;
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
			Uri resourceLocator = new Uri("/Pw.Hub;component/windows/modulesapieditorwindow.xaml", UriKind.Relative);
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
			MainScrollViewer = (ScrollViewer)target;
			MainScrollViewer.ScrollChanged += MainScrollViewer_OnScrollChanged;
			break;
		case 2:
			NameText = (TextBox)target;
			break;
		case 3:
			VersionText = (TextBox)target;
			break;
		case 4:
			ShowPreviewCheck = (CheckBox)target;
			ShowPreviewCheck.Checked += ShowPreviewCheck_OnChanged;
			ShowPreviewCheck.Unchecked += ShowPreviewCheck_OnChanged;
			break;
		case 5:
			((Button)target).Click += Bold_Click;
			break;
		case 6:
			((Button)target).Click += Italic_Click;
			break;
		case 7:
			((Button)target).Click += Header_Click;
			break;
		case 8:
			((Button)target).Click += List_Click;
			break;
		case 9:
			((Button)target).Click += Code_Click;
			break;
		case 10:
			GenerateDescriptionBtn = (Button)target;
			break;
		case 11:
			DescriptionEditor = (TextBox)target;
			DescriptionEditor.TextChanged += DescriptionEditor_OnTextChanged;
			break;
		case 12:
			PreviewContainer = (Border)target;
			break;
		case 13:
			PreviewHeader = (Border)target;
			break;
		case 14:
			PreviewWebView = (WebView2)target;
			break;
		case 15:
			OpenLuaEditorButton = (Button)target;
			OpenLuaEditorButton.Click += OpenLuaEditor_Click;
			break;
		case 16:
			AddInputButton = (Button)target;
			break;
		case 17:
			RemoveInputButton = (Button)target;
			break;
		case 18:
			InputsGrid = (DataGrid)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

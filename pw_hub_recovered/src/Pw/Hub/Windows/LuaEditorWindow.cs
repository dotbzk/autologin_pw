using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Resources;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Extensions.DependencyInjection;
using Pw.Hub.Infrastructure;
using Pw.Hub.Pages;
using Pw.Hub.Services;
using Pw.Hub.Tools;
using Pw.Hub.ViewModels;

namespace Pw.Hub.Windows;

public class LuaEditorWindow : Window, IComponentConnector
{
	private sealed class ObjectType
	{
		public string Name { get; }

		public string[] Fields { get; }

		public ObjectType(string name, params string[] fields)
		{
			Name = name;
			Fields = fields ?? Array.Empty<string>();
		}
	}

	private readonly LuaEditorViewModel _vm = new LuaEditorViewModel();

	private LuaScriptRunner _runner;

	private readonly IDiffPreviewService _diffService;

	private CompletionWindow _completionWindow;

	private bool _isRunning;

	private bool _isDebugging;

	private bool _isSplitterDragging;

	private double _aiPaneLastWidth = 460.0;

	private ModuleArgsWindow _runArgsWindow;

	private static readonly ObjectType AccountType = new ObjectType("Account", "Id", "Name", "Email", "ImageSource", "LastVisit", "Servers", "ImageUri", "SquadId", "Squad");

	private static readonly ObjectType AccountServerType = new ObjectType("AccountServer", "Id", "OptionId", "Name", "DefaultCharacterOptionId", "Characters", "CharactersWithPlaceholder", "AccountId");

	private static readonly ObjectType CharacterType = new ObjectType("AccountCharacter", "Id", "OptionId", "Name", "Server", "ServerId");

	private static readonly ObjectType SquadType = new ObjectType("Squad", "Id", "Name", "Accounts");

	private static readonly Dictionary<string, ObjectType> ParamTypeByName = new Dictionary<string, ObjectType>(StringComparer.Ordinal)
	{
		{ "acc", AccountType },
		{ "Account", AccountType },
		{ "account", AccountType },
		{ "accounts", AccountType },
		{ "Servers", AccountServerType },
		{ "servers", AccountServerType },
		{ "Characters", CharacterType },
		{ "characters", CharacterType },
		{ "Squad", SquadType },
		{ "squad", SquadType },
		{ "result", null },
		{ "exists", null },
		{ "found", null },
		{ "isAuth", null },
		{ "ok", null }
	};

	private readonly ApiSymbol[] _apiSymbols = new ApiSymbol[15]
	{
		new ApiSymbol("Account_GetAccountCb", "Account_GetAccountCb(accountId, function(acc)\r\n    __CURSOR__\r\nend)", "Get an account by ID and return it to callback(acc)"),
		new ApiSymbol("Account_IsAuthorizedCb", "Account_IsAuthorizedCb(accountId, function(isAuth)\r\n    __CURSOR__\r\nend)", "Check account authorization"),
		new ApiSymbol("Account_GetAccountsJsonCb", "Account_GetAccountsJsonCb(function(json)\r\n    __CURSOR__\r\nend)", "Account list (JSON)"),
		new ApiSymbol("Account_GetAccountsCb", "Account_GetAccountsCb(function(accounts)\r\n    __CURSOR__\r\nend)", "Account list (table)"),
		new ApiSymbol("Account_ChangeAccountCb", "Account_ChangeAccountCb(accountId, function(ok)\r\n    __CURSOR__\r\nend)", "Switch the active account"),
		new ApiSymbol("Browser_NavigateCb", "Browser_NavigateCb(url, function()\r\n    __CURSOR__\r\nend)", "Open a URL"),
		new ApiSymbol("Browser_ReloadCb", "Browser_ReloadCb(function()\r\n    __CURSOR__\r\nend)", "Reload the page"),
		new ApiSymbol("Browser_ExecuteScriptCb", "Browser_ExecuteScriptCb(jsCode, function(result)\r\n    __CURSOR__\r\nend)", "Execute JavaScript and return the result"),
		new ApiSymbol("Browser_ElementExistsCb", "Browser_ElementExistsCb(selector, function(exists)\r\n    __CURSOR__\r\nend)", "Check whether an element exists"),
		new ApiSymbol("Browser_WaitForElementCb", "Browser_WaitForElementCb(selector, timeoutMs, function(found)\r\n    __CURSOR__\r\nend)", "Wait for an element"),
		new ApiSymbol("Print", "Print(value)", "Write text to the console"),
		new ApiSymbol("DelayCb", "DelayCb(ms, function()\r\n    __CURSOR__\r\nend)", "Delay with a callback"),
		new ApiSymbol("ReportProgress", "ReportProgress(percent)", "Update progress"),
		new ApiSymbol("ReportProgressMsg", "ReportProgressMsg(percent, message)", "Update progress with a message"),
		new ApiSymbol("Complete", "Complete()", "Complete the task or script")
	};

	private List<ICompletionData> _cachedCompletionItems;

	internal Button RunBtn;

	internal Button DebugBtn;

	internal Button StopBtn;

	internal Button AiToggleBtn;

	internal TextBlock BpCountText;

	internal ColumnDefinition AiPaneColumn;

	internal TextEditor Editor;

	internal ScrollViewer OutputScroll;

	internal TextBox OutputBox;

	internal GridSplitter MainSplitter;

	internal Border AiPaneRoot;

	internal ScrollViewer AiMessagesScroll;

	internal ItemsControl AiMessagesPanel;

	internal RichTextBox AiDiffBox;

	internal TextBox AiInput;

	internal CheckBox AiBraveCheck;

	internal Button AiSendBtn;

	internal Button AiApplyBtn;

	private bool _contentLoaded;

	public List<InputDefinitionDto> ApiInputs { get; set; }

	public LuaEditorWindow(LuaScriptRunner runner)
	{
		_runner = runner;
		_diffService = App.Services?.GetService<IDiffPreviewService>() ?? new DiffPreviewService();
		InitializeComponent();
		base.DataContext = _vm;
		_vm.SetRunner(_runner);
		_vm.SetOwner(this);
		CommonInit();
	}

	public LuaEditorWindow()
	{
		_diffService = App.Services?.GetService<IDiffPreviewService>() ?? new DiffPreviewService();
		InitializeComponent();
		base.DataContext = _vm;
		try
		{
			AccountPage accountPage = (Application.Current?.MainWindow as MainWindow)?.AccountPage;
			LuaScriptRunner luaScriptRunner = ((accountPage != null) ? new LuaScriptRunner(accountPage.AccountManager, accountPage.Browser) : null);
			if (luaScriptRunner != null)
			{
				_runner = luaScriptRunner;
				_vm.SetRunner(_runner);
			}
		}
		catch
		{
		}
		_vm.SetOwner(this);
		CommonInit();
	}

	private void CommonInit()
	{
		BpCountText.SetBinding(TextBlock.TextProperty, new Binding("Breakpoints.Count")
		{
			StringFormat = "({0} breakpoints)"
		});
		_vm.RequestOpenDebugVariables += delegate(int line, IDictionary<string, object> locals, IDictionary<string, object> globals)
		{
			try
			{
				DebugVariablesWindow debugVariablesWindow = new DebugVariablesWindow();
				debugVariablesWindow.Owner = this;
				debugVariablesWindow.SetData(line, locals, globals);
				debugVariablesWindow.ShowDialog();
			}
			catch
			{
			}
		};
		try
		{
			INotifyPropertyChanged notifyPropertyChanged = _vm?.Ai;
			if (notifyPropertyChanged != null)
			{
				notifyPropertyChanged.PropertyChanged += AiOnPropertyChanged;
			}
			try
			{
				_vm.Ai.RequestOpenFullDiff += OnRequestOpenFullDiff;
			}
			catch
			{
			}
		}
		catch
		{
		}
		base.Loaded += delegate
		{
			try
			{
				Editor.Text = _vm.Code ?? Editor.Text;
			}
			catch
			{
			}
			try
			{
				if (Editor != null)
				{
					Editor.TextChanged += delegate
					{
						try
						{
							string text = Editor?.Text ?? string.Empty;
							text = text.Replace("\r\n", "\n");
							_vm.Code = text;
							try
							{
								CommandManager.InvalidateRequerySuggested();
							}
							catch
							{
							}
						}
						catch
						{
						}
					};
				}
			}
			catch
			{
			}
			try
			{
				if (_runner == null)
				{
					AccountPage accountPage = (Application.Current?.MainWindow as MainWindow)?.AccountPage;
					if (accountPage != null)
					{
						_runner = new LuaScriptRunner(accountPage.AccountManager, accountPage.Browser);
						_vm.SetRunner(_runner);
						_vm.SetOwner(this);
						try
						{
							CommandManager.InvalidateRequerySuggested();
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
			try
			{
				if (ApiInputs != null && ApiInputs.Count > 0)
				{
					_vm.Inputs.Clear();
					foreach (InputDefinitionDto apiInput in ApiInputs)
					{
						_vm.Inputs.Add(apiInput);
					}
				}
			}
			catch
			{
			}
			try
			{
				Editor?.Focus();
			}
			catch
			{
			}
		};
		base.Loaded += OnLoaded;
		base.Closed += OnClosed;
	}

	public void SetCode(string code)
	{
		try
		{
			string text = code ?? string.Empty;
			try
			{
				_vm.Code = text;
			}
			catch
			{
			}
			if (Editor != null)
			{
				Editor.Text = text;
			}
		}
		catch
		{
		}
	}

	public string GetCode()
	{
		try
		{
			return Editor?.Text ?? string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		try
		{
			StreamResourceInfo resourceStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/LuaEditor/lua.xshd", UriKind.Absolute));
			if (resourceStream != null)
			{
				using XmlTextReader reader = new XmlTextReader(resourceStream.Stream);
				Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
			}
			else
			{
				string path = Path.Combine(AppContext.BaseDirectory, "Assets", "LuaEditor", "lua.xshd");
				if (File.Exists(path))
				{
					using FileStream input = File.OpenRead(path);
					using XmlTextReader reader2 = new XmlTextReader(input);
					Editor.SyntaxHighlighting = HighlightingLoader.Load(reader2, HighlightingManager.Instance);
				}
				else
				{
					AppendLog("Could not find lua.xshd in resources or the output directory.");
				}
			}
		}
		catch (Exception ex)
		{
			AppendLog("Syntax highlighting load error: " + ex.Message);
		}
		if (Editor != null && string.IsNullOrWhiteSpace(Editor.Text))
		{
			Editor.Text = "-- Lua script\n-- Use Account_*Cb and Browser_*Cb APIs\nPrint('Hello from editor')\n";
		}
		Editor.TextArea.TextEntering += TextAreaOnTextEntering;
		Editor.TextArea.TextEntered += TextAreaOnTextEntered;
		Editor.PreviewKeyDown += EditorOnPreviewKeyDown;
	}

	private void OnClosed(object sender, EventArgs e)
	{
		_runner.SetPrintSink(null);
		try
		{
			_runner.Stop();
		}
		catch
		{
		}
		try
		{
			INotifyPropertyChanged notifyPropertyChanged = _vm?.Ai;
			if (notifyPropertyChanged != null)
			{
				notifyPropertyChanged.PropertyChanged -= AiOnPropertyChanged;
			}
			try
			{
				if (_vm?.Ai != null)
				{
					_vm.Ai.RequestOpenFullDiff -= OnRequestOpenFullDiff;
				}
			}
			catch
			{
			}
		}
		catch
		{
		}
		try
		{
			if (_runArgsWindow != null)
			{
				_runArgsWindow.Close();
				_runArgsWindow = null;
			}
		}
		catch
		{
		}
	}

	private void EditorOnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
		{
			ShowCompletion();
			e.Handled = true;
		}
	}

	private void TextAreaOnTextEntered(object sender, TextCompositionEventArgs e)
	{
		if (e.Text == ".")
		{
			ShowCompletion();
		}
	}

	private void TextAreaOnTextEntering(object sender, TextCompositionEventArgs e)
	{
		if (_completionWindow != null && e.Text.Length > 0 && !char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
		{
			_completionWindow.CompletionList.RequestInsertion(e);
		}
	}

	private void ShowCompletion()
	{
		try
		{
			if (_completionWindow != null && _completionWindow.IsVisible)
			{
				return;
			}
			List<ICompletionData> list = TryBuildMemberCompletionItems();
			if (_cachedCompletionItems == null)
			{
				IReadOnlyList<LuaApiRegistry.Entry> all = LuaApiRegistry.GetAll();
				_cachedCompletionItems = new List<ICompletionData>(Math.Max(1, all.Count));
				foreach (LuaApiRegistry.Entry item in all)
				{
					string signature = (string.IsNullOrWhiteSpace(item.Snippet) ? (item.Signature ?? item.Name) : item.Snippet);
					ApiSymbol api = new ApiSymbol(item.Name, signature, item.Description ?? string.Empty);
					_cachedCompletionItems.Add(new SignatureCompletionData(api));
				}
			}
			_completionWindow = new CompletionWindow(Editor.TextArea)
			{
				SizeToContent = SizeToContent.WidthAndHeight,
				Owner = this
			};
			try
			{
				Brush background = (TryFindResource("BackgroundSecondaryBrush") as Brush) ?? Brushes.White;
				Brush foreground = (TryFindResource("TextPrimaryBrush") as Brush) ?? Brushes.Black;
				Brush borderBrush = (TryFindResource("BorderBrush") as Brush) ?? Brushes.Gray;
				FontFamily fontFamily = (TryFindResource("MonospaceFont") as FontFamily) ?? new FontFamily("Consolas");
				_completionWindow.Background = background;
				_completionWindow.BorderBrush = borderBrush;
				CompletionList completionList = _completionWindow.CompletionList;
				completionList.Background = background;
				completionList.Foreground = foreground;
				completionList.BorderBrush = borderBrush;
				completionList.FontFamily = fontFamily;
				completionList.FontSize = Editor.FontSize;
			}
			catch
			{
			}
			IList<ICompletionData> completionData = _completionWindow.CompletionList.CompletionData;
			completionData.Clear();
			foreach (ICompletionData item2 in (list != null && list.Count > 0) ? list : _cachedCompletionItems)
			{
				completionData.Add(item2);
			}
			_completionWindow.Closed += delegate
			{
				_completionWindow = null;
			};
			_completionWindow.Show();
		}
		catch
		{
		}
	}

	private List<ICompletionData> TryBuildMemberCompletionItems()
	{
		try
		{
			if (Editor?.TextArea == null)
			{
				return null;
			}
			Caret caret = Editor.TextArea.Caret;
			int num = Math.Max(0, Math.Min(Editor.Document.TextLength, caret.Offset));
			if (num == 0)
			{
				return null;
			}
			if (Editor.Document.GetCharAt(Math.Max(0, num - 1)) != '.')
			{
				return null;
			}
			int num2 = num - 1;
			string text = Editor.Document.Text;
			int num3 = num2;
			int num4;
			for (num4 = num3; num4 > 0; num4--)
			{
				char c = text[num4 - 1];
				if (!char.IsLetterOrDigit(c) && c != '_')
				{
					break;
				}
			}
			if (num4 < num3)
			{
				string text2 = text.Substring(num4, num3 - num4);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					if (ParamTypeByName.TryGetValue(text2, out var value) && value != null)
					{
						return BuildFieldItemsForType(value);
					}
					int num5 = Math.Max(0, num4 - 200);
					string input = text.Substring(num5, num4 - num5);
					Match match = Regex.Match(input, "function\\s*\\(\\s*([a-zA-Z_][a-zA-Z0-9_]*)\\s*\\)\\s*$", RegexOptions.RightToLeft);
					if (match.Success)
					{
						string value2 = match.Groups[1].Value;
						if (string.Equals(value2, text2, StringComparison.Ordinal))
						{
							Match match2 = Regex.Match(input, "([A-Za-z_][A-Za-z0-9_]*)\\s*\\(.*function\\s*\\(\\s*" + Regex.Escape(value2) + "\\s*\\)", RegexOptions.Singleline);
							if (match2.Success)
							{
								ObjectType objectType = InferTypeFromApiCallback(match2.Groups[1].Value);
								if (objectType != null)
								{
									return BuildFieldItemsForType(objectType);
								}
							}
						}
					}
				}
			}
			int num6 = num2 - 1;
			while (num6 >= 0 && char.IsWhiteSpace(text[num6]))
			{
				num6--;
			}
			if (num6 >= 0 && text[num6] == ']')
			{
				int num7 = num6 - 1;
				while (num7 >= 0 && text[num7] != '[' && text[num7] != '\n' && text[num7] != '\r')
				{
					num7--;
				}
				if (num7 >= 1 && text[num7] == '[')
				{
					int num8 = num7;
					int num9;
					for (num9 = num8; num9 > 0; num9--)
					{
						char c2 = text[num9 - 1];
						if (!char.IsLetterOrDigit(c2) && c2 != '_')
						{
							break;
						}
					}
					if (num9 < num8)
					{
						string text3 = text.Substring(num9, num8 - num9);
						if (!string.IsNullOrWhiteSpace(text3) && ParamTypeByName.TryGetValue(text3, out var value3) && value3 != null)
						{
							return BuildFieldItemsForType(value3);
						}
					}
				}
			}
			return null;
		}
		catch
		{
			return null;
		}
	}

	private static ObjectType InferTypeFromApiCallback(string apiName)
	{
		if (apiName == "Account_GetAccountCb")
		{
			return AccountType;
		}
		return null;
	}

	private static List<ICompletionData> BuildFieldItemsForType(ObjectType type)
	{
		List<ICompletionData> list = new List<ICompletionData>(type.Fields.Length);
		string[] fields = type.Fields;
		foreach (string fieldName in fields)
		{
			list.Add(new FieldCompletionData(fieldName, type.Name));
		}
		return list;
	}

	private void AppendLog(string text)
	{
		try
		{
			if (!base.Dispatcher.CheckAccess())
			{
				base.Dispatcher.BeginInvoke(new Action<string>(AppendLog), text);
				return;
			}
			string text2 = $"[{DateTime.Now:HH:mm:ss}] {text}";
			if (OutputBox.Text.Length == 0)
			{
				OutputBox.Text = text2;
			}
			else
			{
				OutputBox.AppendText("\r\n" + text2);
			}
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				try
				{
					OutputBox.CaretIndex = OutputBox.Text?.Length ?? 0;
					OutputBox.UpdateLayout();
					OutputBox.ScrollToEnd();
					try
					{
						OutputScroll?.ScrollToBottom();
					}
					catch
					{
					}
				}
				catch
				{
				}
			}, DispatcherPriority.Background);
		}
		catch
		{
		}
	}

	private void OnCloseClick(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void OnAiToggleClick(object sender, RoutedEventArgs e)
	{
		try
		{
			if (_isSplitterDragging)
			{
				return;
			}
			if (AiPaneRoot.Visibility != Visibility.Visible)
			{
				if (AiPaneColumn.ActualWidth > 10.0)
				{
					_aiPaneLastWidth = AiPaneColumn.ActualWidth;
				}
				AiPaneColumn.MinWidth = 600.0;
				AiPaneColumn.Width = new GridLength(Math.Max(600.0, _aiPaneLastWidth));
				AiPaneRoot.Visibility = Visibility.Visible;
			}
			else
			{
				if (AiPaneColumn.ActualWidth > 10.0)
				{
					_aiPaneLastWidth = AiPaneColumn.ActualWidth;
				}
				AiPaneRoot.Visibility = Visibility.Collapsed;
				AiPaneColumn.MinWidth = 0.0;
				AiPaneColumn.Width = new GridLength(0.0);
			}
		}
		catch
		{
		}
	}

	private void AiOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		try
		{
			if (!(e.PropertyName == "DiffLines") && !string.IsNullOrEmpty(e.PropertyName))
			{
				return;
			}
			IList<string> list = _vm?.Ai?.DiffLines;
			if (list == null || list.Count <= 0)
			{
				return;
			}
			if (!base.Dispatcher.CheckAccess())
			{
				base.Dispatcher.BeginInvoke(new Action<object, PropertyChangedEventArgs>(AiOnPropertyChanged), sender, e);
			}
			else if (AiPaneRoot.Visibility != Visibility.Visible)
			{
				if (AiPaneColumn.ActualWidth > 10.0)
				{
					_aiPaneLastWidth = AiPaneColumn.ActualWidth;
				}
				AiPaneColumn.MinWidth = 600.0;
				AiPaneColumn.Width = new GridLength(Math.Max(600.0, _aiPaneLastWidth));
				AiPaneRoot.Visibility = Visibility.Visible;
			}
		}
		catch
		{
		}
	}

	private void OnRequestOpenFullDiff(IList<string> lines)
	{
		try
		{
			if (!base.Dispatcher.CheckAccess())
			{
				base.Dispatcher.BeginInvoke(new Action<IList<string>>(OnRequestOpenFullDiff), lines);
			}
			else
			{
				DiffPreviewWindow diffPreviewWindow = new DiffPreviewWindow(_vm?.Ai);
				diffPreviewWindow.Owner = this;
				diffPreviewWindow.ShowDialog();
			}
		}
		catch
		{
		}
	}

	private void OnSplitterDragStarted(object sender, DragStartedEventArgs e)
	{
		_isSplitterDragging = true;
		try
		{
			if (AiToggleBtn != null)
			{
				AiToggleBtn.IsEnabled = false;
			}
		}
		catch
		{
		}
	}

	private void OnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
	{
		_isSplitterDragging = false;
		try
		{
			if (AiToggleBtn != null)
			{
				AiToggleBtn.IsEnabled = true;
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
			Uri resourceLocator = new Uri("/Pw.Hub;component/windows/luaeditorwindow.xaml", UriKind.Relative);
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
			RunBtn = (Button)target;
			break;
		case 2:
			DebugBtn = (Button)target;
			break;
		case 3:
			StopBtn = (Button)target;
			break;
		case 4:
			AiToggleBtn = (Button)target;
			AiToggleBtn.Click += OnAiToggleClick;
			break;
		case 5:
			((Button)target).Click += OnCloseClick;
			break;
		case 6:
			BpCountText = (TextBlock)target;
			break;
		case 7:
			AiPaneColumn = (ColumnDefinition)target;
			break;
		case 8:
			Editor = (TextEditor)target;
			break;
		case 9:
			OutputScroll = (ScrollViewer)target;
			break;
		case 10:
			OutputBox = (TextBox)target;
			break;
		case 11:
			MainSplitter = (GridSplitter)target;
			MainSplitter.DragStarted += OnSplitterDragStarted;
			MainSplitter.DragCompleted += OnSplitterDragCompleted;
			break;
		case 12:
			AiPaneRoot = (Border)target;
			break;
		case 13:
			AiMessagesScroll = (ScrollViewer)target;
			break;
		case 14:
			AiMessagesPanel = (ItemsControl)target;
			break;
		case 15:
			AiDiffBox = (RichTextBox)target;
			break;
		case 16:
			AiInput = (TextBox)target;
			break;
		case 17:
			AiBraveCheck = (CheckBox)target;
			break;
		case 18:
			AiSendBtn = (Button)target;
			break;
		case 19:
			AiApplyBtn = (Button)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

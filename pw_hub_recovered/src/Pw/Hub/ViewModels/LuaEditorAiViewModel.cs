using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Pw.Hub.Infrastructure;
using Pw.Hub.Services;
using Pw.Hub.Windows;

namespace Pw.Hub.ViewModels;

public class LuaEditorAiViewModel : BaseViewModel
{
	private readonly IAiAssistantService _ai;

	private readonly IDiffPreviewService _diff;

	private IList<string> _diffLines = new List<string>();

	private string _inputText = string.Empty;

	private bool _braveMode;

	private bool _isBusy;

	private bool _canApply;

	private string _lastProposedCode = string.Empty;

	private readonly StringBuilder _assistantRaw = new StringBuilder();

	private string _lastPreviewCode = string.Empty;

	public Func<string> GetCurrentCode { get; set; } = () => string.Empty;

	public Action<string> ApplyCode { get; set; } = delegate
	{
	};

	internal LuaEditorViewModel ParentVm { get; set; }

	public ObservableCollection<AiChatMessage> Messages { get; } = new ObservableCollection<AiChatMessage>();

	public IList<string> DiffLines
	{
		get
		{
			return _diffLines;
		}
		private set
		{
			_diffLines = value ?? new List<string>();
			OnPropertyChanged("DiffLines");
			UpdateApplyAvailability();
		}
	}

	public string InputText
	{
		get
		{
			return _inputText;
		}
		set
		{
			_inputText = value ?? string.Empty;
			OnPropertyChanged("InputText");
			OnPropertyChanged("CanSend");
			CommandManager.InvalidateRequerySuggested();
		}
	}

	public bool BraveMode
	{
		get
		{
			return _braveMode;
		}
		set
		{
			_braveMode = value;
			OnPropertyChanged("BraveMode");
			if (_braveMode)
			{
				TryAutoApply();
			}
		}
	}

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

	public bool CanApply
	{
		get
		{
			return _canApply;
		}
		private set
		{
			_canApply = value;
			OnPropertyChanged("CanApply");
			CommandManager.InvalidateRequerySuggested();
		}
	}

	public ICommand SendCommand { get; }

	public ICommand NewSessionCommand { get; }

	public ICommand ApplyCommand { get; }

	public ICommand OpenFullDiffCommand { get; }

	private bool CanSend
	{
		get
		{
			if (!IsBusy)
			{
				return !string.IsNullOrWhiteSpace(InputText);
			}
			return false;
		}
	}

	public event Action<IList<string>> RequestOpenFullDiff;

	public LuaEditorAiViewModel()
	{
		_ai = (App.Services?.GetService(typeof(IAiAssistantService)) as IAiAssistantService) ?? new AiAssistantService(new DiffPreviewService());
		_diff = (App.Services?.GetService(typeof(IDiffPreviewService)) as IDiffPreviewService) ?? new DiffPreviewService();
		SendCommand = new RelayCommand(async delegate
		{
			await SendAsync();
		}, (object _) => CanSend);
		NewSessionCommand = new RelayCommand(delegate
		{
			NewSession();
		}, (object _) => !IsBusy);
		ApplyCommand = new RelayCommand(delegate
		{
			Apply();
		}, (object _) => CanApply);
		OpenFullDiffCommand = new RelayCommand(delegate
		{
			this.RequestOpenFullDiff?.Invoke(DiffLines);
		}, (object _) => true);
	}

	private static string StripCodeBlocks(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		try
		{
			return Regex.Replace(text, "```[\0-\uffff]*?```", string.Empty, RegexOptions.Singleline).Trim();
		}
		catch
		{
			return text;
		}
	}

	private static string ExtractLuaCodeBlock(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		try
		{
			Match match = Regex.Match(text, "```lua\\s*(.*?)```", RegexOptions.IgnoreCase | RegexOptions.Singleline);
			if (match.Success)
			{
				return match.Groups[1].Value.Trim();
			}
			match = Regex.Match(text, "```\\s*(.*?)```", RegexOptions.Singleline);
			if (match.Success)
			{
				return match.Groups[1].Value.Trim();
			}
			return string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	public void NotifySystem(string text)
	{
		if (!string.IsNullOrWhiteSpace(text))
		{
			Messages.Add(new AiChatMessage
			{
				Role = "system",
				Text = text
			});
		}
	}

	private void AddSystem(string text)
	{
		Messages.Add(new AiChatMessage
		{
			Role = "system",
			Text = text
		});
	}

	private void AddUser(string text)
	{
		Messages.Add(new AiChatMessage
		{
			Role = "user",
			Text = text
		});
	}

	private void AddAssistant(string text)
	{
		Messages.Add(new AiChatMessage
		{
			Role = "assistant",
			Text = text
		});
	}

	private void NewSession()
	{
		if (IsBusy)
		{
			return;
		}
		Messages.Clear();
		DiffLines = new List<string>();
		_lastProposedCode = string.Empty;
		_lastPreviewCode = string.Empty;
		_assistantRaw.Clear();
		_ai.NewSession();
		try
		{
			string text = LuaApiRegistry.ToCheatSheetText();
			if (!string.IsNullOrWhiteSpace(text))
			{
				AddSystem(text);
			}
		}
		catch
		{
		}
		AddSystem("Новая сессия начата. Опишите задачу для AI.");
		RequeryCommands();
	}

	private async Task SendAsync(CancellationToken ct = default(CancellationToken))
	{
		if (!CanSend)
		{
			RequeryCommands();
			return;
		}
		IsBusy = true;
		string currentCode;
		AiChatMessage assistantMsg;
		try
		{
			IAiConfigService aiConfigService = (App.Services?.GetService(typeof(IAiConfigService)) as IAiConfigService) ?? new AiConfigService();
			if (string.IsNullOrWhiteSpace((aiConfigService.GetEffective().ApiKey ?? string.Empty).Trim()))
			{
				try
				{
					if (App.Services?.GetService(typeof(IWindowService)) is IWindowService windowService)
					{
						AiApiKeyWindow window = new AiApiKeyWindow();
						Window owner = Application.Current?.Windows?.OfType<Window>()?.FirstOrDefault((Window w) => w.IsActive) ?? Application.Current?.MainWindow;
						windowService.ShowDialog(window, owner);
					}
					else
					{
						AiApiKeyWindow aiApiKeyWindow = new AiApiKeyWindow();
						aiApiKeyWindow.Owner = Application.Current?.MainWindow;
						aiApiKeyWindow.ShowDialog();
					}
				}
				catch
				{
				}
				if (string.IsNullOrWhiteSpace((aiConfigService.GetEffective().ApiKey ?? string.Empty).Trim()))
				{
					NotifySystem("AI API key не задан. Откройте настройки AI и введите ключ для продолжения.");
					return;
				}
			}
			_assistantRaw.Clear();
			_lastPreviewCode = string.Empty;
			DiffLines = new List<string>();
			string text = InputText.Trim();
			AddUser(text);
			InputText = string.Empty;
			currentCode = (GetCurrentCode?.Invoke() ?? string.Empty).Replace("\r\n", "\n");
			string manualChangesDiff = null;
			try
			{
				if (ParentVm != null && !string.IsNullOrEmpty(ParentVm.LastCodeSentToAi))
				{
					string text2 = ParentVm.LastCodeSentToAi.Replace("\r\n", "\n");
					if (!string.Equals(currentCode, text2, StringComparison.Ordinal))
					{
						IList<string> list = _diff.BuildUnifiedDiffGit(text2, currentCode);
						if (list != null && list.Count > 0)
						{
							manualChangesDiff = string.Join("\n", list);
						}
					}
				}
			}
			catch
			{
			}
			assistantMsg = new AiChatMessage
			{
				Role = "assistant",
				Text = string.Empty,
				ShowDiffButton = true
			};
			Messages.Add(assistantMsg);
			AiAssistantResponse aiAssistantResponse = await _ai.SendAsync(text, currentCode, manualChangesDiff, ct, OnDelta);
			try
			{
				if (ParentVm != null)
				{
					ParentVm.LastCodeSentToAi = currentCode;
				}
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(aiAssistantResponse.AssistantText))
			{
				string text3 = StripCodeBlocks(aiAssistantResponse.AssistantText);
				if (!string.Equals(assistantMsg.Text, text3, StringComparison.Ordinal))
				{
					assistantMsg.Text = text3;
				}
			}
			if (!string.IsNullOrWhiteSpace(aiAssistantResponse.Summary))
			{
				Messages.Add(new AiChatMessage
				{
					Role = "assistant",
					Text = aiAssistantResponse.Summary,
					IsSummary = true,
					ShowDiffButton = true
				});
			}
			if (aiAssistantResponse.DiffLines != null && aiAssistantResponse.DiffLines.Count > 0)
			{
				DiffLines = aiAssistantResponse.DiffLines;
			}
			_lastProposedCode = aiAssistantResponse.ExtractedCode?.Replace("\r\n", "\n") ?? _lastProposedCode;
			UpdateApplyAvailability();
			if (BraveMode)
			{
				TryAutoApply();
			}
		}
		catch (Exception ex)
		{
			AddAssistant("Ошибка: " + ex.Message);
		}
		finally
		{
			IsBusy = false;
			RequeryCommands();
		}
		void OnDelta(string chunk)
		{
			if (string.IsNullOrEmpty(chunk))
			{
				return;
			}
			try
			{
				Dispatcher dispatcher = Application.Current?.Dispatcher;
				if (dispatcher == null || dispatcher.CheckAccess())
				{
					Apply();
				}
				else
				{
					dispatcher.BeginInvoke(new Action(Apply));
				}
			}
			catch
			{
			}
			void Apply()
			{
				_assistantRaw.Append(chunk);
				string text4 = StripCodeBlocks(_assistantRaw.ToString());
				assistantMsg.Text = text4;
				string text5 = ExtractLuaCodeBlock(_assistantRaw.ToString());
				if (!string.IsNullOrEmpty(text5) && !string.Equals(text5, _lastPreviewCode, StringComparison.Ordinal))
				{
					_lastPreviewCode = text5;
					_lastProposedCode = text5.Replace("\r\n", "\n");
					try
					{
						IList<string> list2 = _diff.BuildUnifiedDiffGit(currentCode, _lastProposedCode);
						DiffLines = list2 ?? new List<string>();
						UpdateApplyAvailability();
					}
					catch
					{
					}
				}
			}
		}
	}

	private void RequeryCommands()
	{
		try
		{
			OnPropertyChanged("CanSend");
		}
		catch
		{
		}
		try
		{
			CommandManager.InvalidateRequerySuggested();
		}
		catch
		{
		}
	}

	private void UpdateApplyAvailability()
	{
		string a = (GetCurrentCode?.Invoke() ?? string.Empty).Replace("\r\n", "\n");
		CanApply = !string.IsNullOrEmpty(_lastProposedCode) && !string.Equals(a, _lastProposedCode, StringComparison.Ordinal);
	}

	private void TryAutoApply()
	{
		if (BraveMode && CanApply)
		{
			Apply();
			AddSystem("AI brave: изменения автоматически применены к редактору.");
		}
	}

	private void Apply()
	{
		if (!CanApply)
		{
			return;
		}
		ApplyCode?.Invoke(_lastProposedCode);
		try
		{
			if (ParentVm != null)
			{
				ParentVm.LastCodeSentToAi = _lastProposedCode;
			}
		}
		catch
		{
		}
		UpdateApplyAvailability();
	}
}

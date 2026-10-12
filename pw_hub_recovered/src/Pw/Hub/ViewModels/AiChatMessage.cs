namespace Pw.Hub.ViewModels;

public sealed class AiChatMessage : BaseViewModel
{
	private string _role = string.Empty;

	private string _text = string.Empty;

	private bool _isSummary;

	private bool _showDiffButton;

	public string Role
	{
		get
		{
			return _role;
		}
		set
		{
			_role = value ?? string.Empty;
			OnPropertyChanged("Role");
		}
	}

	public string Text
	{
		get
		{
			return _text;
		}
		set
		{
			_text = value ?? string.Empty;
			OnPropertyChanged("Text");
		}
	}

	public bool IsSummary
	{
		get
		{
			return _isSummary;
		}
		set
		{
			_isSummary = value;
			OnPropertyChanged("IsSummary");
		}
	}

	public bool ShowDiffButton
	{
		get
		{
			return _showDiffButton;
		}
		set
		{
			_showDiffButton = value;
			OnPropertyChanged("ShowDiffButton");
		}
	}
}

using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Pw.Hub.Services;

namespace Pw.Hub.Windows;

public class AiApiKeyWindow : Window, IComponentConnector
{
	private readonly IAiConfigService _cfg;

	internal TextBox UrlBox;

	internal TextBox ModelBox;

	internal PasswordBox KeyBox;

	private bool _contentLoaded;

	public AiApiKeyWindow()
	{
		InitializeComponent();
		_cfg = (App.Services?.GetService(typeof(IAiConfigService)) as IAiConfigService) ?? new AiConfigService();
		try
		{
			AiConfig aiConfig = _cfg.Load();
			UrlBox.Text = aiConfig.ApiUrl ?? "https://ollama.com/api/chat";
			ModelBox.Text = aiConfig.Model ?? "llama3.1";
		}
		catch
		{
		}
	}

	private void OnCancel(object sender, RoutedEventArgs e)
	{
		try
		{
			base.DialogResult = false;
		}
		catch
		{
		}
		Close();
	}

	private void OnSave(object sender, RoutedEventArgs e)
	{
		try
		{
			string text = (UrlBox.Text ?? string.Empty).Trim();
			string text2 = (ModelBox.Text ?? string.Empty).Trim();
			string apiKey = (KeyBox.Password ?? string.Empty).Trim();
			AiConfig cfg = new AiConfig
			{
				ApiUrl = (string.IsNullOrWhiteSpace(text) ? "https://ollama.com/api/chat" : text),
				Model = (string.IsNullOrWhiteSpace(text2) ? "llama3.1" : text2),
				ApiKey = apiKey
			};
			_cfg.Save(cfg);
			try
			{
				base.DialogResult = true;
			}
			catch
			{
			}
			Close();
		}
		catch
		{
			try
			{
				base.DialogResult = false;
			}
			catch
			{
			}
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
			Uri resourceLocator = new Uri("/Pw.Hub;component/windows/aiapikeywindow.xaml", UriKind.Relative);
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
			UrlBox = (TextBox)target;
			break;
		case 2:
			ModelBox = (TextBox)target;
			break;
		case 3:
			KeyBox = (PasswordBox)target;
			break;
		case 4:
			((Button)target).Click += OnCancel;
			break;
		case 5:
			((Button)target).Click += OnSave;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

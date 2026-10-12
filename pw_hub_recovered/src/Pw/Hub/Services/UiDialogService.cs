using System;
using System.Collections.Generic;
using System.Windows;
using Pw.Hub.Windows;

namespace Pw.Hub.Services;

public class UiDialogService : IUiDialogService
{
	public void Alert(string message, string caption = "PW Hub")
	{
		try
		{
			MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch
		{
		}
	}

	public bool Confirm(string message, string caption = "Подтверждение")
	{
		try
		{
			return MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
		}
		catch
		{
			return false;
		}
	}

	public string? Prompt(string prompt, string caption = "Ввод", string? defaultValue = null)
	{
		return null;
	}

	public Dictionary<string, object>? AskRunArguments(IList<InputDefinitionDto> inputs)
	{
		try
		{
			if (inputs == null || inputs.Count == 0)
			{
				return new Dictionary<string, object>();
			}
			ModuleArgsWindow moduleArgsWindow = new ModuleArgsWindow(inputs)
			{
				Owner = Application.Current?.MainWindow
			};
			if (moduleArgsWindow.ShowDialog() == true)
			{
				Dictionary<string, object> dictionary = ((moduleArgsWindow.Values != null) ? new Dictionary<string, object>(moduleArgsWindow.Values, StringComparer.Ordinal) : new Dictionary<string, object>(StringComparer.Ordinal));
				try
				{
					foreach (InputDefinitionDto input in inputs)
					{
						string text = (input?.Type ?? "string").ToLowerInvariant();
						if (text == "enum" || text == "перечисление")
						{
							string text2 = input?.Name ?? string.Empty;
							if (!string.IsNullOrWhiteSpace(text2) && (!dictionary.TryGetValue(text2, out var value) || value == null || (value is string value2 && string.IsNullOrWhiteSpace(value2))) && moduleArgsWindow.StringValues != null && moduleArgsWindow.StringValues.TryGetValue(text2, out var value3))
							{
								dictionary[text2] = value3 ?? string.Empty;
							}
						}
					}
				}
				catch
				{
				}
				return dictionary;
			}
			return null;
		}
		catch
		{
			return null;
		}
	}

	public Dictionary<string, object>? AskRunArguments(IList<InputDefinitionDto> inputs, ref ModuleArgsWindow window, Window owner)
	{
		try
		{
			if (inputs == null || inputs.Count == 0)
			{
				return new Dictionary<string, object>();
			}
			window = new ModuleArgsWindow(inputs)
			{
				Owner = (owner ?? Application.Current?.MainWindow)
			};
			if (window.ShowDialog() == true)
			{
				Dictionary<string, object> dictionary = ((window.Values != null) ? new Dictionary<string, object>(window.Values, StringComparer.Ordinal) : new Dictionary<string, object>(StringComparer.Ordinal));
				try
				{
					foreach (InputDefinitionDto input in inputs)
					{
						string text = (input?.Type ?? "string").ToLowerInvariant();
						if (text == "enum" || text == "перечисление")
						{
							string text2 = input?.Name ?? string.Empty;
							if (!string.IsNullOrWhiteSpace(text2) && (!dictionary.TryGetValue(text2, out var value) || value == null || (value is string value2 && string.IsNullOrWhiteSpace(value2))) && window.StringValues != null && window.StringValues.TryGetValue(text2, out var value3))
							{
								dictionary[text2] = value3 ?? string.Empty;
							}
						}
					}
				}
				catch
				{
				}
				return dictionary;
			}
			return null;
		}
		catch
		{
			return null;
		}
	}
}

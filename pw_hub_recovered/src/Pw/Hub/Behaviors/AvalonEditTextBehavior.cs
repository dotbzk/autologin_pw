using System;
using System.Windows;
using System.Windows.Data;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace Pw.Hub.Behaviors;

public static class AvalonEditTextBehavior
{
	public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached("Text", typeof(string), typeof(AvalonEditTextBehavior), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

	public static void SetText(DependencyObject element, string value)
	{
		element.SetValue(TextProperty, value);
	}

	public static string GetText(DependencyObject element)
	{
		return (string)element.GetValue(TextProperty);
	}

	private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d is TextEditor textEditor))
		{
			return;
		}
		textEditor.TextChanged -= EditorOnTextChanged;
		string text = (e.NewValue as string) ?? string.Empty;
		if (!string.Equals(textEditor.Text, text, StringComparison.Ordinal))
		{
			if (textEditor.Document == null)
			{
				textEditor.Document = new TextDocument();
			}
			textEditor.Document.Text = text;
		}
		textEditor.TextChanged += EditorOnTextChanged;
	}

	private static void EditorOnTextChanged(object? sender, EventArgs e)
	{
		if (sender is TextEditor textEditor)
		{
			BindingExpression bindingExpression = BindingOperations.GetBindingExpression(textEditor, TextProperty);
			if (bindingExpression != null)
			{
				SetText(textEditor, textEditor.Text);
				bindingExpression.UpdateSource();
			}
		}
	}
}

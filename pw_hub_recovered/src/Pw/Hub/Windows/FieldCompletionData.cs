using System;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace Pw.Hub.Windows;

internal class FieldCompletionData : ICompletionData
{
	public ImageSource Image => null;

	public string Text { get; }

	public object Content { get; }

	public object Description { get; }

	public double Priority { get; set; }

	public FieldCompletionData(string fieldName, string ownerType)
	{
		Text = fieldName;
		Content = fieldName;
		Description = ownerType + "." + fieldName;
	}

	public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
	{
		textArea.Document.Replace(completionSegment, Text);
		int val = completionSegment.Offset + Text.Length;
		int val2 = textArea.Document?.TextLength ?? 0;
		textArea.Caret.Offset = Math.Max(0, Math.Min(val2, val));
	}
}

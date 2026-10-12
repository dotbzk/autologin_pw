using System;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace Pw.Hub.Windows;

internal class SignatureCompletionData : ICompletionData
{
	public ApiSymbol Api { get; }

	public ImageSource Image => null;

	public string Text { get; }

	public object Content { get; }

	public object Description { get; }

	public double Priority { get; set; }

	public SignatureCompletionData(ApiSymbol api)
	{
		Api = api;
		Text = api.Name;
		Content = api.Signature;
		Description = (string.IsNullOrWhiteSpace(api.Description) ? api.Signature : (api.Description + "\n" + api.Signature));
	}

	public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
	{
		string text = Api.Signature ?? Api.Name;
		string text2 = "__CURSOR__";
		int num = text.IndexOf(text2, StringComparison.Ordinal);
		string text3 = ((num >= 0) ? text.Replace(text2, string.Empty) : text);
		textArea.Document.Replace(completionSegment, text3);
		int val;
		if (num >= 0)
		{
			val = completionSegment.Offset + num;
		}
		else
		{
			int num2 = text3.IndexOf('(');
			int num3 = text3.LastIndexOf(')');
			if (num2 >= 0)
			{
				int num4 = completionSegment.Offset + Math.Min(num2 + 1, text3.Length);
				val = ((num3 > num2 + 1) ? num4 : (completionSegment.Offset + text3.Length));
			}
			else
			{
				val = completionSegment.Offset + text3.Length;
			}
		}
		int val2 = textArea.Document?.TextLength ?? 0;
		int offset = Math.Max(0, Math.Min(val2, val));
		textArea.Caret.Offset = offset;
	}
}

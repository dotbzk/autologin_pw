using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Pw.Hub.Behaviors;

public static class RichTextBoxDiffBehavior
{
	public static readonly DependencyProperty LinesProperty = DependencyProperty.RegisterAttached("Lines", typeof(IList<string>), typeof(RichTextBoxDiffBehavior), new PropertyMetadata(null, OnLinesChanged));

	public static void SetLines(DependencyObject element, IList<string> value)
	{
		element.SetValue(LinesProperty, value);
	}

	public static IList<string> GetLines(DependencyObject element)
	{
		return (IList<string>)element.GetValue(LinesProperty);
	}

	private static void OnLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d is RichTextBox richTextBox))
		{
			return;
		}
		try
		{
			FlowDocument flowDocument = new FlowDocument
			{
				PagePadding = new Thickness(4.0)
			};
			FontFamily fontFamily = (Application.Current.TryFindResource("MonospaceFont") as FontFamily) ?? new FontFamily("Consolas");
			flowDocument.FontFamily = fontFamily;
			flowDocument.FontSize = 13.0;
			foreach (string item in (e.NewValue as IList<string>) ?? Array.Empty<string>())
			{
				Paragraph paragraph = new Paragraph
				{
					Margin = new Thickness(0.0),
					LineHeight = 16.0
				};
				Run run = new Run(item ?? string.Empty);
				if (item.StartsWith("+++ ") || item.StartsWith("--- ") || item.StartsWith("@@"))
				{
					run.Foreground = (Application.Current.TryFindResource("TextSecondaryBrush") as Brush) ?? Brushes.Gray;
					run.FontWeight = FontWeights.SemiBold;
				}
				else if (item.StartsWith("+"))
				{
					run.Foreground = Brushes.Green;
				}
				else if (item.StartsWith("-"))
				{
					run.Foreground = Brushes.IndianRed;
				}
				else
				{
					run.Foreground = (Application.Current.TryFindResource("TextPrimaryBrush") as Brush) ?? Brushes.Black;
				}
				paragraph.Inlines.Add(run);
				flowDocument.Blocks.Add(paragraph);
			}
			if (flowDocument.Blocks.Count == 0)
			{
				flowDocument.Blocks.Add(new Paragraph(new Run(""))
				{
					Margin = new Thickness(0.0)
				});
			}
			richTextBox.Document = flowDocument;
		}
		catch
		{
			try
			{
				richTextBox.Document = new FlowDocument(new Paragraph(new Run("Не удалось отобразить diff")));
			}
			catch
			{
			}
		}
	}
}

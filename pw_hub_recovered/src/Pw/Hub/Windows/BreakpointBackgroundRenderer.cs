using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace Pw.Hub.Windows;

internal class BreakpointBackgroundRenderer : IBackgroundRenderer
{
	private readonly ISet<int> _breakpoints;

	private readonly Brush _fill;

	private readonly Pen _borderPen;

	public KnownLayer Layer => KnownLayer.Background;

	public BreakpointBackgroundRenderer(ISet<int> breakpoints)
	{
		_breakpoints = breakpoints;
		_fill = new SolidColorBrush(Color.FromArgb(60, 220, 60, 60));
		_fill.Freeze();
		_borderPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 200, 50, 50)), 1.0);
		_borderPen.Freeze();
	}

	public void Draw(TextView textView, DrawingContext drawingContext)
	{
		if (textView?.VisualLines == null || textView.VisualLines.Count == 0)
		{
			return;
		}
		foreach (VisualLine visualLine in textView.VisualLines)
		{
			DocumentLine documentLine = visualLine.FirstDocumentLine;
			while (documentLine != null && documentLine.Offset <= visualLine.LastDocumentLine.EndOffset)
			{
				if (_breakpoints.Contains(documentLine.LineNumber))
				{
					foreach (Rect item in BackgroundGeometryBuilder.GetRectsForSegment(textView, documentLine))
					{
						if (!(item.Width <= 0.0) && !(item.Height <= 0.0))
						{
							drawingContext.DrawRectangle(rectangle: new Rect(0.0, item.Top, Math.Max(textView.ActualWidth, item.Right), item.Height), brush: _fill, pen: _borderPen);
						}
					}
				}
				if (documentLine == visualLine.LastDocumentLine)
				{
					break;
				}
				documentLine = documentLine.NextLine;
			}
		}
	}
}

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Pw.Hub.Infrastructure;

public sealed class InsertionAdorner : Adorner
{
	private readonly Pen _pen;

	private readonly Pen _shadowPen;

	private readonly double _margin;

	public bool PositionAbove { get; set; }

	public InsertionAdorner(UIElement adornedElement)
		: base(adornedElement)
	{
		base.IsHitTestVisible = false;
		Brush brush = (TryFindResource("AccentHighlightBrush") as Brush) ?? new SolidColorBrush(Color.FromRgb(51, 153, byte.MaxValue));
		SolidColorBrush brush2 = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0));
		_pen = new Pen(brush, 2.0);
		_pen.Freeze();
		_shadowPen = new Pen(brush2, 4.0);
		_shadowPen.Freeze();
		_margin = 66.0;
	}

	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		double actualWidth = base.ActualWidth;
		double actualHeight = base.ActualHeight;
		if (!(actualWidth <= 0.0) && !(actualHeight <= 0.0))
		{
			double num = (PositionAbove ? 0.5 : (actualHeight - 0.5));
			Point point = new Point(_margin, num);
			Point point2 = new Point(actualWidth - 8.0, num);
			dc.DrawLine(_shadowPen, new Point(point.X, num + (double)(PositionAbove ? 1 : (-1))), new Point(point2.X, num + (double)(PositionAbove ? 1 : (-1))));
			dc.DrawLine(_pen, point, point2);
			int num2 = 6;
			double num3 = (PositionAbove ? (num + 1.0) : (num - 1.0));
			dc.DrawLine(_pen, new Point(point.X, num3), new Point(point.X, num3 + (double)(PositionAbove ? num2 : (-num2))));
		}
	}
}

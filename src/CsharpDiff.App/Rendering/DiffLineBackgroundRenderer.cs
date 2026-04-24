using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace CsharpDiff.App.Rendering;

public enum LineMark
{
    None,
    Added,
    Removed,
    Changed,
    Gap,
}

public sealed class DiffLineBackgroundRenderer : IBackgroundRenderer
{
    private IReadOnlyList<LineMark> _marks = System.Array.Empty<LineMark>();

    public KnownLayer Layer => KnownLayer.Background;

    private static readonly IBrush AddedBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0xC0, 0x40));
    private static readonly IBrush RemovedBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xE0, 0x40, 0x40));
    private static readonly IBrush ChangedBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xC8, 0x00));
    private static readonly IBrush GapBrush = BuildGapBrush();

    private static IBrush BuildGapBrush()
    {
        var geom = new DrawingGroup();
        using (var ctx = geom.Open())
        {
            var bg = new SolidColorBrush(Color.FromArgb(0x30, 0x80, 0x80, 0x80));
            var stripe = new SolidColorBrush(Color.FromArgb(0x28, 0x80, 0x80, 0x80));
            ctx.DrawRectangle(bg, null, new Rect(0, 0, 8, 8));
            var pen = new Pen(stripe, 2);
            ctx.DrawLine(pen, new Point(-2, 8), new Point(8, -2));
            ctx.DrawLine(pen, new Point(0, 10), new Point(10, 0));
        }
        return new DrawingBrush
        {
            Drawing = geom,
            TileMode = TileMode.Tile,
            SourceRect = new RelativeRect(0, 0, 8, 8, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(0, 0, 8, 8, RelativeUnit.Absolute),
        };
    }

    public void SetMarks(IReadOnlyList<LineMark> marks)
    {
        _marks = marks;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_marks.Count == 0 || textView.Document is null) return;

        foreach (var vl in textView.VisualLines)
        {
            var lineNumber = vl.FirstDocumentLine.LineNumber;
            if (lineNumber < 1 || lineNumber > _marks.Count) continue;
            var mark = _marks[lineNumber - 1];
            if (mark == LineMark.None) continue;

            var brush = mark switch
            {
                LineMark.Added => AddedBrush,
                LineMark.Removed => RemovedBrush,
                LineMark.Changed => ChangedBrush,
                LineMark.Gap => GapBrush,
                _ => null,
            };
            if (brush is null) continue;

            foreach (var rc in BackgroundGeometryBuilder.GetRectsFromVisualSegment(
                         textView, vl, 0, 1000))
            {
                var r = new Rect(0, rc.Top, textView.Bounds.Width, rc.Height);
                drawingContext.FillRectangle(brush, r);
            }
        }
    }
}

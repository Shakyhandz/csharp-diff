using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace CsharpDiff.App.Rendering;

public sealed class GapAwareLineNumberMargin : AbstractMargin
{
    private IReadOnlyList<int> _rowToOrig = System.Array.Empty<int>();
    private Typeface _typeface;
    private Typeface _boldTypeface;
    private double _emSize = 12;
    private int _maxDigits = 1;
    private int _highlightStartRow = -1;
    private int _highlightEndRow = -1;

    public GapAwareLineNumberMargin()
    {
        _typeface = new Typeface("Consolas");
        _boldTypeface = new Typeface("Consolas", FontStyle.Normal, FontWeight.Bold);
    }

    public void SetMap(IReadOnlyList<int> rowToOriginalLineNumber)
    {
        _rowToOrig = rowToOriginalLineNumber;
        _maxDigits = 1;
        foreach (var n in _rowToOrig)
            if (n > 0)
            {
                var d = (int)Math.Floor(Math.Log10(n)) + 1;
                if (d > _maxDigits) _maxDigits = d;
            }
        InvalidateMeasure();
        InvalidateVisual();
    }

    public void SetHighlightRows(int startRow, int endRow)
    {
        _highlightStartRow = startRow;
        _highlightEndRow = endRow;
        InvalidateVisual();
    }

    protected override void OnTextViewChanged(TextView? oldTextView, TextView? newTextView)
    {
        if (oldTextView != null) oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
        if (newTextView != null)
        {
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
            var family = newTextView.GetValue(TextElement.FontFamilyProperty);
            _typeface = new Typeface(family);
            _boldTypeface = new Typeface(family, FontStyle.Normal, FontWeight.Bold);
            _emSize = newTextView.GetValue(TextElement.FontSizeProperty);
        }
        base.OnTextViewChanged(oldTextView, newTextView);
        InvalidateMeasure();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override Size MeasureOverride(Size availableSize)
    {
        var sample = new string('9', Math.Max(_maxDigits, 2));
        var ft = new FormattedText(sample, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, _typeface, _emSize, Brushes.Transparent);
        return new Size(ft.Width + 8, 0);
    }

    public override void Render(DrawingContext context)
    {
        var tv = TextView;
        if (tv is null || !tv.VisualLinesValid) return;
        var foreground = GetValue(TemplatedControl.ForegroundProperty) ?? Brushes.Gray;
        foreach (var vl in tv.VisualLines)
        {
            var idx = vl.FirstDocumentLine.LineNumber - 1;
            if (idx < 0 || idx >= _rowToOrig.Count) continue;
            var orig = _rowToOrig[idx];
            if (orig <= 0) continue;
            var row = idx + 1;
            var highlighted = _highlightStartRow > 0
                              && row >= _highlightStartRow
                              && row <= _highlightEndRow;
            var ft = new FormattedText(
                orig.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                highlighted ? _boldTypeface : _typeface,
                _emSize,
                highlighted ? Brushes.Red : foreground);
            var y = vl.VisualTop - tv.VerticalOffset;
            var x = Bounds.Width - ft.Width - 2;
            context.DrawText(ft, new Point(x, y));
        }
    }
}

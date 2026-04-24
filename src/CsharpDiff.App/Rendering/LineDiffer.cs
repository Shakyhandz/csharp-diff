using System.Collections.Generic;
using System.Text;
using CsharpDiff.Core;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace CsharpDiff.App.Rendering;

public static class LineDiffer
{
    public sealed record Result(
        string LeftAligned,
        string RightAligned,
        IReadOnlyList<LineMark> LeftMarks,
        IReadOnlyList<LineMark> RightMarks,
        IReadOnlyList<int> LeftLineNumbers,
        IReadOnlyList<int> RightLineNumbers);

    public static Result Compute(string? left, string? right,
        bool ignoreUsings = false, bool ignoreWhitespace = false)
    {
        left ??= string.Empty;
        right ??= string.Empty;

        var model = SideBySideDiffBuilder.Diff(left, right, ignoreWhiteSpace: ignoreWhitespace);

        var leftText = new StringBuilder();
        var rightText = new StringBuilder();
        var leftMarks = new List<LineMark>();
        var rightMarks = new List<LineMark>();
        var leftNums = new List<int>();
        var rightNums = new List<int>();

        var leftOrig = 0;
        var rightOrig = 0;
        var rowCount = System.Math.Max(model.OldText.Lines.Count, model.NewText.Lines.Count);

        for (var i = 0; i < rowCount; i++)
        {
            var l = i < model.OldText.Lines.Count ? model.OldText.Lines[i] : null;
            var r = i < model.NewText.Lines.Count ? model.NewText.Lines[i] : null;

            if (i > 0) { leftText.Append('\n'); rightText.Append('\n'); }

            if (l is null || l.Type == ChangeType.Imaginary)
            {
                leftMarks.Add(LineMark.Gap);
                leftNums.Add(0);
            }
            else
            {
                leftText.Append(l.Text);
                leftOrig++;
                leftNums.Add(leftOrig);
                leftMarks.Add(l.Type switch
                {
                    ChangeType.Deleted => LineMark.Removed,
                    ChangeType.Modified => LineMark.Changed,
                    ChangeType.Inserted => LineMark.Added,
                    _ => LineMark.None,
                });
            }

            if (r is null || r.Type == ChangeType.Imaginary)
            {
                rightMarks.Add(LineMark.Gap);
                rightNums.Add(0);
            }
            else
            {
                rightText.Append(r.Text);
                rightOrig++;
                rightNums.Add(rightOrig);
                rightMarks.Add(r.Type switch
                {
                    ChangeType.Inserted => LineMark.Added,
                    ChangeType.Modified => LineMark.Changed,
                    ChangeType.Deleted => LineMark.Removed,
                    _ => LineMark.None,
                });
            }
        }

        if (ignoreUsings)
        {
            MaskUsings(left, leftMarks, leftNums);
            MaskUsings(right, rightMarks, rightNums);
        }

        return new Result(
            leftText.ToString(),
            rightText.ToString(),
            leftMarks,
            rightMarks,
            leftNums,
            rightNums);
    }

    private static void MaskUsings(string text, List<LineMark> marks, List<int> origLineNumbers)
    {
        var usingLines = UsingScanner.FindUsingLines(text);
        if (usingLines.Count == 0) return;
        for (var i = 0; i < marks.Count; i++)
        {
            var orig = origLineNumbers[i];
            if (orig > 0 && usingLines.Contains(orig - 1))
                marks[i] = LineMark.None;
        }
    }
}

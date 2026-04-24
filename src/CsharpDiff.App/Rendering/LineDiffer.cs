using System.Collections.Generic;
using CsharpDiff.Core;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace CsharpDiff.App.Rendering;

public static class LineDiffer
{
    public sealed record Result(IReadOnlyList<LineMark> LeftMarks, IReadOnlyList<LineMark> RightMarks);

    public static Result Compute(string? left, string? right,
        bool ignoreUsings = false, bool ignoreWhitespace = false)
    {
        left ??= string.Empty;
        right ??= string.Empty;

        var model = SideBySideDiffBuilder.Diff(left, right, ignoreWhiteSpace: ignoreWhitespace);

        var leftMarks = new List<LineMark>();
        foreach (var line in model.OldText.Lines)
        {
            if (line.Type == ChangeType.Imaginary) continue;
            leftMarks.Add(line.Type switch
            {
                ChangeType.Deleted => LineMark.Removed,
                ChangeType.Modified => LineMark.Changed,
                ChangeType.Inserted => LineMark.Added,
                _ => LineMark.None,
            });
        }

        var rightMarks = new List<LineMark>();
        foreach (var line in model.NewText.Lines)
        {
            if (line.Type == ChangeType.Imaginary) continue;
            rightMarks.Add(line.Type switch
            {
                ChangeType.Inserted => LineMark.Added,
                ChangeType.Modified => LineMark.Changed,
                ChangeType.Deleted => LineMark.Removed,
                _ => LineMark.None,
            });
        }

        if (ignoreUsings)
        {
            MaskUsings(left, leftMarks);
            MaskUsings(right, rightMarks);
        }

        return new Result(leftMarks, rightMarks);
    }

    private static void MaskUsings(string text, List<LineMark> marks)
    {
        var usingLines = UsingScanner.FindUsingLines(text);
        foreach (var line in usingLines)
            if (line >= 0 && line < marks.Count)
                marks[line] = LineMark.None;
    }
}

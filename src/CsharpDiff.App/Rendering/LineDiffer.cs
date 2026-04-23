using System.Collections.Generic;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace CsharpDiff.App.Rendering;

public static class LineDiffer
{
    public sealed record Result(IReadOnlyList<LineMark> LeftMarks, IReadOnlyList<LineMark> RightMarks);

    public static Result Compute(string? left, string? right)
    {
        left ??= string.Empty;
        right ??= string.Empty;

        var model = SideBySideDiffBuilder.Diff(left, right);

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

        return new Result(leftMarks, rightMarks);
    }
}

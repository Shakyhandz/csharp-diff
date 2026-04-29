using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CsharpDiff.Core;

public sealed record FilePair(FileFingerprint? Left, FileFingerprint? Right);

public static class FileMatcher
{
    public static List<FilePair> Match(
        IReadOnlyList<FileFingerprint> left,
        IReadOnlyList<FileFingerprint> right,
        double threshold = 0.3)
    {
        var pairs = new List<FilePair>();
        var usedLeft = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var usedRight = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        // Phase 1: exact relative-path match — highest priority.
        var rightByRel = new Dictionary<string, FileFingerprint>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var r in right) rightByRel[r.RelativePath] = r;

        foreach (var l in left)
        {
            if (rightByRel.TryGetValue(l.RelativePath, out var r) && !usedRight.Contains(r.AbsolutePath))
            {
                pairs.Add(new FilePair(l, r));
                usedLeft.Add(l.AbsolutePath);
                usedRight.Add(r.AbsolutePath);
            }
        }

        // Phase 2: Jaccard symbol-overlap match on remaining files.
        var remainingLeft = left.Where(f => !usedLeft.Contains(f.AbsolutePath)).ToList();
        var remainingRight = right.Where(f => !usedRight.Contains(f.AbsolutePath)).ToList();

        var candidates = new List<(double score, FileFingerprint l, FileFingerprint r)>();
        foreach (var l in remainingLeft)
            foreach (var r in remainingRight)
            {
                if (!string.Equals(l.Extension, r.Extension, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                var score = Jaccard(l.Symbols, r.Symbols);
                if (score > 0) candidates.Add((score, l, r));
            }

        candidates.Sort((a, b) =>
        {
            var c = b.score.CompareTo(a.score);
            if (c != 0) return c;
            var aName = Path.GetFileName(a.l.AbsolutePath) == Path.GetFileName(a.r.AbsolutePath);
            var bName = Path.GetFileName(b.l.AbsolutePath) == Path.GetFileName(b.r.AbsolutePath);
            return bName.CompareTo(aName);
        });

        foreach (var (score, l, r) in candidates)
        {
            if (score < threshold) break;
            if (usedLeft.Contains(l.AbsolutePath) || usedRight.Contains(r.AbsolutePath)) continue;
            pairs.Add(new FilePair(l, r));
            usedLeft.Add(l.AbsolutePath);
            usedRight.Add(r.AbsolutePath);
        }

        // Phase 3: filename fallback for files with empty or disjoint symbol sets.
        var unmatchedLeft = left.Where(f => !usedLeft.Contains(f.AbsolutePath)).ToList();
        var unmatchedRight = right.Where(f => !usedRight.Contains(f.AbsolutePath)).ToList();

        foreach (var l in unmatchedLeft.ToList())
        {
            var leftName = Path.GetFileName(l.AbsolutePath);
            var hit = unmatchedRight.FirstOrDefault(r =>
                string.Equals(r.Extension, l.Extension, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.GetFileName(r.AbsolutePath), leftName, System.StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                pairs.Add(new FilePair(l, hit));
                unmatchedLeft.Remove(l);
                unmatchedRight.Remove(hit);
            }
        }

        // Phase 4: orphans.
        foreach (var l in unmatchedLeft) pairs.Add(new FilePair(l, null));
        foreach (var r in unmatchedRight) pairs.Add(new FilePair(null, r));
        return pairs;
    }

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var intersect = 0;
        foreach (var s in a) if (b.Contains(s)) intersect++;
        var union = a.Count + b.Count - intersect;
        return union == 0 ? 0 : (double)intersect / union;
    }
}

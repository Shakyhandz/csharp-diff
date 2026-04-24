using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsharpDiff.Core;

public static class UsingScanner
{
    /// Return the set of zero-based line numbers that are part of a
    /// using directive (not using statements or declarations inside methods).
    public static HashSet<int> FindUsingLines(string text)
    {
        var lines = new HashSet<int>();
        if (string.IsNullOrEmpty(text)) return lines;

        var tree = CSharpSyntaxTree.ParseText(text);
        var root = tree.GetRoot();
        foreach (var u in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var span = u.GetLocation().GetLineSpan();
            for (var i = span.StartLinePosition.Line; i <= span.EndLinePosition.Line; i++)
                lines.Add(i);
        }
        return lines;
    }
}

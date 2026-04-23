using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsharpDiff.Core;

public sealed record DiffOptions(
    bool IgnoreWhitespace = true,
    bool IgnoreComments = true)
{
    public static DiffOptions Default { get; } = new();
}

public static class Normalizer
{
    /// Return text of a declaration, normalized according to options.
    /// Used for equality comparison, not for display.
    public static string Normalize(SyntaxNode node, DiffOptions options)
    {
        var working = node;

        if (options.IgnoreComments)
            working = (SyntaxNode)new TriviaStripper().Visit(working)!;

        if (options.IgnoreWhitespace)
            working = working.NormalizeWhitespace();

        var text = working.ToFullString();
        return text.Trim();
    }

    /// Canonicalize a set of partial declarations by concatenating their
    /// normalized forms in a stable order (by original file + span).
    public static string NormalizeMany(IEnumerable<SyntaxNode> nodes, DiffOptions options)
    {
        var ordered = nodes
            .OrderBy(n => n.SyntaxTree.FilePath, StringComparer.Ordinal)
            .ThenBy(n => n.SpanStart)
            .Select(n => Normalize(n, options));
        return string.Join("\n", ordered);
    }

    /// Return the raw display text for a declaration (preserving original formatting).
    public static string DisplayText(IEnumerable<SyntaxNode> nodes)
    {
        return string.Join("\n\n// ---\n\n",
            nodes
                .OrderBy(n => n.SyntaxTree.FilePath, StringComparer.Ordinal)
                .ThenBy(n => n.SpanStart)
                .Select(n => n.ToFullString().TrimEnd()));
    }

    private sealed class TriviaStripper : CSharpSyntaxRewriter
    {
        public TriviaStripper() : base(visitIntoStructuredTrivia: false) { }

        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
        {
            return trivia.Kind() switch
            {
                SyntaxKind.SingleLineCommentTrivia or
                SyntaxKind.MultiLineCommentTrivia or
                SyntaxKind.SingleLineDocumentationCommentTrivia or
                SyntaxKind.MultiLineDocumentationCommentTrivia
                    => default,
                _ => trivia
            };
        }
    }
}

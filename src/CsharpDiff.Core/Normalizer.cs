using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsharpDiff.Core;

public sealed record DiffOptions(
    bool IgnoreUsings = true,
    bool IgnoreWhitespace = true,
    bool IgnoreComments = true,
    bool IncludeCs = true,
    bool IncludeXaml = true,
    bool IncludeOther = false)
{
    public static DiffOptions Default { get; } = new();
}

public static class Normalizer
{
    /// Parse and normalize a raw C# file's text according to options.
    /// Returns empty when input is empty. If no option is on, input is returned as-is.
    public static string NormalizeText(string text, DiffOptions options)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        if (!(options.IgnoreUsings || options.IgnoreWhitespace || options.IgnoreComments))
            return text;

        var tree = CSharpSyntaxTree.ParseText(text);
        return Normalize(tree.GetRoot(), options);
    }

    /// Return text of a declaration, normalized according to options.
    /// Used for equality comparison, not for display.
    public static string Normalize(SyntaxNode node, DiffOptions options)
    {
        var working = node;

        if (options.IgnoreComments)
            working = (SyntaxNode)new TriviaStripper().Visit(working)!;

        if (options.IgnoreUsings)
            working = (SyntaxNode)new UsingRemover().Visit(working)!;

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

    private sealed class UsingRemover : CSharpSyntaxRewriter
    {
        private static readonly SyntaxList<UsingDirectiveSyntax> Empty = SyntaxFactory.List<UsingDirectiveSyntax>();

        public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node) =>
            base.VisitCompilationUnit(node.WithUsings(Empty));

        public override SyntaxNode? VisitNamespaceDeclaration(NamespaceDeclarationSyntax node) =>
            base.VisitNamespaceDeclaration(node.WithUsings(Empty));

        public override SyntaxNode? VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node) =>
            base.VisitFileScopedNamespaceDeclaration(node.WithUsings(Empty));
    }
}

using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsharpDiff.Core;

public sealed class FileFingerprint
{
    public required string AbsolutePath { get; init; }
    public required string RelativePath { get; init; }
    public required SyntaxTree Tree { get; init; }
    public required HashSet<string> Symbols { get; init; }
}

public static class FingerprintBuilder
{
    public static FileFingerprint Build(SyntaxTree tree, string rootFolder)
    {
        var symbols = new HashSet<string>(System.StringComparer.Ordinal);
        Walk(tree.GetRoot(), currentNamespace: "", parentTypeKey: null, symbols);
        var abs = tree.FilePath;
        var rel = Path.GetRelativePath(rootFolder, abs);
        return new FileFingerprint
        {
            AbsolutePath = abs,
            RelativePath = rel,
            Tree = tree,
            Symbols = symbols,
        };
    }

    private static void Walk(SyntaxNode node, string currentNamespace, string? parentTypeKey, HashSet<string> set)
    {
        switch (node)
        {
            case BaseNamespaceDeclarationSyntax nsd:
                {
                    var newNs = string.IsNullOrEmpty(currentNamespace)
                        ? nsd.Name.ToString()
                        : currentNamespace + "." + nsd.Name.ToString();
                    foreach (var child in nsd.ChildNodes()) Walk(child, newNs, parentTypeKey, set);
                    return;
                }
            case TypeDeclarationSyntax td:
                {
                    var name = td.Identifier.Text;
                    if (td.TypeParameterList is { } tp && tp.Parameters.Count > 0)
                        name += "`" + tp.Parameters.Count;
                    var key = ParentQualify(parentTypeKey, currentNamespace, name);
                    set.Add("type:" + key);
                    foreach (var member in td.Members) Walk(member, currentNamespace, key, set);
                    return;
                }
            case EnumDeclarationSyntax ed:
                set.Add("type:" + ParentQualify(parentTypeKey, currentNamespace, ed.Identifier.Text));
                return;
            case DelegateDeclarationSyntax dd:
                set.Add("type:" + ParentQualify(parentTypeKey, currentNamespace, dd.Identifier.Text));
                return;
            case MethodDeclarationSyntax m when parentTypeKey is not null:
                {
                    var arity = m.TypeParameterList?.Parameters.Count ?? 0;
                    var sig = m.Identifier.Text + (arity > 0 ? "`" + arity : "") + ParamList(m.ParameterList);
                    set.Add("m:" + parentTypeKey + "::" + sig);
                    return;
                }
            case ConstructorDeclarationSyntax c when parentTypeKey is not null:
                set.Add("m:" + parentTypeKey + "::.ctor" + ParamList(c.ParameterList));
                return;
            case PropertyDeclarationSyntax p when parentTypeKey is not null:
                set.Add("m:" + parentTypeKey + "::" + p.Identifier.Text);
                return;
            case IndexerDeclarationSyntax ix when parentTypeKey is not null:
                set.Add("m:" + parentTypeKey + "::this" + ParamList(ix.ParameterList));
                return;
            case EventDeclarationSyntax e when parentTypeKey is not null:
                set.Add("m:" + parentTypeKey + "::" + e.Identifier.Text);
                return;
            case EventFieldDeclarationSyntax ef when parentTypeKey is not null:
                foreach (var v in ef.Declaration.Variables)
                    set.Add("m:" + parentTypeKey + "::" + v.Identifier.Text);
                return;
            case FieldDeclarationSyntax f when parentTypeKey is not null:
                foreach (var v in f.Declaration.Variables)
                    set.Add("m:" + parentTypeKey + "::" + v.Identifier.Text);
                return;
            default:
                foreach (var child in node.ChildNodes()) Walk(child, currentNamespace, parentTypeKey, set);
                return;
        }
    }

    private static string ParentQualify(string? parent, string ns, string name) =>
        parent is not null ? parent + "+" + name : (string.IsNullOrEmpty(ns) ? name : ns + "." + name);

    private static string ParamList(ParameterListSyntax list) =>
        "(" + string.Join(",", ParamTypes(list)) + ")";

    private static string ParamList(BracketedParameterListSyntax list) =>
        "[" + string.Join(",", ParamTypes(list)) + "]";

    private static IEnumerable<string> ParamTypes(BaseParameterListSyntax list)
    {
        foreach (var p in list.Parameters) yield return p.Type?.ToString() ?? "?";
    }
}

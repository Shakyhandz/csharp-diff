using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CsharpDiff.Core;

public sealed record LoadedFile(string AbsolutePath, string Extension, SyntaxTree? Tree);

public static class ProjectLoader
{
    public const string CsExt = ".cs";
    public const string AxamlExt = ".axaml";
    public const string XamlExt = ".xaml";

    public static readonly IReadOnlySet<string> XamlExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AxamlExt, XamlExt };

    public static readonly IReadOnlySet<string> KnownExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CsExt, AxamlExt, XamlExt };

    private static readonly string[] ExcludedDirs = { "bin", "obj", ".git", ".vs", "node_modules" };

    public static IReadOnlyList<SyntaxTree> LoadFolder(string folder)
    {
        var only = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CsExt };
        return LoadFiles(folder, only, includeOther: false)
            .Where(f => f.Tree is not null)
            .Select(f => f.Tree!)
            .ToList();
    }

    public static IReadOnlyList<LoadedFile> LoadFiles(
        string folder,
        IReadOnlySet<string> includedExtensions,
        bool includeOther)
    {
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException(folder);

        var files = new List<LoadedFile>();
        foreach (var path in EnumerateAllFiles(folder))
        {
            var ext = Path.GetExtension(path);
            var inCatalog = KnownExtensions.Contains(ext);
            var keep = inCatalog
                ? includedExtensions.Contains(ext)
                : includeOther;
            if (!keep) continue;

            if (string.Equals(ext, CsExt, StringComparison.OrdinalIgnoreCase))
            {
                var text = File.ReadAllText(path);
                var tree = CSharpSyntaxTree.ParseText(text, path: path);
                files.Add(new LoadedFile(path, ext, tree));
            }
            else
            {
                files.Add(new LoadedFile(path, ext, Tree: null));
            }
        }
        return files;
    }

    public static HashSet<string> FindProjectDirectories(string folder)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(folder)) return result;

        foreach (var csproj in EnumerateFilesFiltered(folder, "*.csproj"))
        {
            var dir = Path.GetDirectoryName(csproj) ?? folder;
            var rel = Path.GetRelativePath(folder, dir).Replace('\\', '/');
            if (rel == ".") rel = "";
            result.Add(rel);
        }
        return result;
    }

    private static IEnumerable<string> EnumerateAllFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(sub);
                if (ExcludedDirs.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                stack.Push(sub);
            }
            foreach (var file in Directory.EnumerateFiles(dir))
                yield return file;
        }
    }

    private static IEnumerable<string> EnumerateFilesFiltered(string root, string pattern)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(sub);
                if (ExcludedDirs.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                stack.Push(sub);
            }
            foreach (var file in Directory.EnumerateFiles(dir, pattern))
                yield return file;
        }
    }
}

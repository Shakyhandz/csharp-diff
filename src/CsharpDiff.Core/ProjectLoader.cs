using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CsharpDiff.Core;

public static class ProjectLoader
{
    private static readonly string[] ExcludedDirs = { "bin", "obj", ".git", ".vs", "node_modules" };

    public static IReadOnlyList<SyntaxTree> LoadFolder(string folder)
    {
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException(folder);

        var trees = new List<SyntaxTree>();
        foreach (var path in EnumerateCsFiles(folder))
        {
            var text = File.ReadAllText(path);
            var tree = CSharpSyntaxTree.ParseText(text, path: path);
            trees.Add(tree);
        }
        return trees;
    }

    private static IEnumerable<string> EnumerateCsFiles(string root)
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
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
                yield return file;
        }
    }
}

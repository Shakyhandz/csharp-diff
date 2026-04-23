using CsharpDiff.Core;
using Xunit;

namespace CsharpDiff.Core.Tests;

public class ProjectDiffTests : IDisposable
{
    private readonly string _left;
    private readonly string _right;

    public ProjectDiffTests()
    {
        _left = Path.Combine(Path.GetTempPath(), "csd_" + Guid.NewGuid().ToString("N") + "_L");
        _right = Path.Combine(Path.GetTempPath(), "csd_" + Guid.NewGuid().ToString("N") + "_R");
        Directory.CreateDirectory(_left);
        Directory.CreateDirectory(_right);
    }

    public void Dispose()
    {
        TryDelete(_left);
        TryDelete(_right);
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }

    private void WriteLeft(string relativePath, string content) => WriteUnder(_left, relativePath, content);
    private void WriteRight(string relativePath, string content) => WriteUnder(_right, relativePath, content);

    private static void WriteUnder(string root, string name, string content)
    {
        var full = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static DiffNode? FindFile(DiffNode root, string displayName)
    {
        if (root.Kind == NodeKind.File && root.DisplayName == displayName) return root;
        foreach (var c in root.Children)
        {
            var hit = FindFile(c, displayName);
            if (hit is not null) return hit;
        }
        return null;
    }

    [Fact]
    public void Same_content_different_folder_matches_as_unchanged()
    {
        WriteLeft("debug/main.cs", "namespace N; public class C { public void M() { } }");
        WriteRight("debug_net9/main.cs", "namespace N; public class C { public void M() { } }");

        var root = ProjectDiff.Compare(_left, _right);
        var file = FindFile(root, "main.cs");
        Assert.NotNull(file);
        Assert.Equal(DiffStatus.Unchanged, file!.Status);
        Assert.EndsWith("debug\\main.cs", file.LeftFilePath!.Replace('/', '\\'));
        Assert.EndsWith("debug_net9\\main.cs", file.RightFilePath!.Replace('/', '\\'));
    }

    [Fact]
    public void Same_path_modified_content_is_flagged()
    {
        WriteLeft("a.cs", "namespace N; public class C { public int M() { return 1; } }");
        WriteRight("a.cs", "namespace N; public class C { public int M() { return 2; } }");

        var root = ProjectDiff.Compare(_left, _right);
        var file = FindFile(root, "a.cs");
        Assert.NotNull(file);
        Assert.Equal(DiffStatus.Modified, file!.Status);
    }

    [Fact]
    public void Left_only_file_is_removed()
    {
        WriteLeft("orphan.cs", "namespace N; public class O { }");

        var root = ProjectDiff.Compare(_left, _right);
        var file = FindFile(root, "orphan.cs");
        Assert.NotNull(file);
        Assert.Equal(DiffStatus.Removed, file!.Status);
        Assert.Null(file.RightFilePath);
    }

    [Fact]
    public void Right_only_file_shows_under_added_bucket()
    {
        WriteRight("new.cs", "namespace N; public class New { }");

        var root = ProjectDiff.Compare(_left, _right);
        var file = FindFile(root, "new.cs");
        Assert.NotNull(file);
        Assert.Equal(DiffStatus.Added, file!.Status);
        Assert.Null(file.LeftFilePath);
    }

    [Fact]
    public void Whitespace_only_change_is_unchanged()
    {
        WriteLeft("a.cs", "namespace N; public class C { public void M() { if (true) return; } }");
        WriteRight("a.cs", """
            namespace N;
            public class C
            {
                public void M()
                {
                    if (true)
                        return;
                }
            }
            """);

        var root = ProjectDiff.Compare(_left, _right);
        var file = FindFile(root, "a.cs");
        Assert.NotNull(file);
        Assert.Equal(DiffStatus.Unchanged, file!.Status);
    }

    [Fact]
    public void Filename_fallback_matches_empty_symbol_files()
    {
        // Files with no declarations should still match by filename.
        WriteLeft("empty.cs", "// only a comment\n");
        WriteRight("empty.cs", "// still only a comment\n");

        var root = ProjectDiff.Compare(_left, _right);
        var file = FindFile(root, "empty.cs");
        Assert.NotNull(file);
        // Comment stripped by default options → Unchanged.
        Assert.Equal(DiffStatus.Unchanged, file!.Status);
    }

    [Fact]
    public void Rename_with_same_content_matches_via_symbol_overlap()
    {
        WriteLeft("old-name.cs", "namespace N; public class UniqueClass { public void Foo() { } public int Bar() => 1; }");
        WriteRight("completely-different.cs", "namespace N; public class UniqueClass { public void Foo() { } public int Bar() => 1; }");

        var root = ProjectDiff.Compare(_left, _right);
        var file = FindFile(root, "old-name.cs");
        Assert.NotNull(file);
        Assert.Equal(DiffStatus.Unchanged, file!.Status);
        Assert.EndsWith("completely-different.cs", file.RightFilePath);
    }
}

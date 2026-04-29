using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CsharpDiff.Core;

public static class ProjectDiff
{
    private const string AddedOnlyFolderName = "(new in right)";

    public static DiffNode Compare(string leftFolder, string rightFolder, DiffOptions? options = null)
    {
        options ??= DiffOptions.Default;

        var includedExts = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (options.IncludeCs) includedExts.Add(ProjectLoader.CsExt);
        if (options.IncludeXaml)
            foreach (var ext in ProjectLoader.XamlExtensions)
                includedExts.Add(ext);

        var leftFiles = ProjectLoader.LoadFiles(leftFolder, includedExts, options.IncludeOther);
        var rightFiles = ProjectLoader.LoadFiles(rightFolder, includedExts, options.IncludeOther);

        var leftFprints = leftFiles.Select(f => f.Tree is not null
            ? FingerprintBuilder.Build(f.Tree, leftFolder)
            : FingerprintBuilder.BuildPlain(f.AbsolutePath, leftFolder)).ToList();
        var rightFprints = rightFiles.Select(f => f.Tree is not null
            ? FingerprintBuilder.Build(f.Tree, rightFolder)
            : FingerprintBuilder.BuildPlain(f.AbsolutePath, rightFolder)).ToList();

        var pairs = FileMatcher.Match(leftFprints, rightFprints);

        var leftProjDirs = ProjectLoader.FindProjectDirectories(leftFolder);
        var rightProjDirs = ProjectLoader.FindProjectDirectories(rightFolder);
        var projectDirs = new HashSet<string>(leftProjDirs, System.StringComparer.OrdinalIgnoreCase);
        projectDirs.UnionWith(rightProjDirs);

        var root = new DiffNode
        {
            Kind = projectDirs.Contains("") ? NodeKind.Project : NodeKind.Folder,
            DisplayName = "<root>",
            Key = "",
        };

        // 1. Main tree = left-folder hierarchy.
        var folderIndex = new Dictionary<string, DiffNode>(System.StringComparer.OrdinalIgnoreCase)
        {
            [""] = root,
        };

        foreach (var pair in pairs)
        {
            if (pair.Left is null) continue; // added-only goes in its own bucket below

            var relPath = pair.Left.RelativePath;
            var folderRel = Path.GetDirectoryName(relPath) ?? "";
            var folderNode = EnsureFolder(folderRel.Replace('\\', '/'), folderIndex, root, projectDirs);

            var fileNode = BuildFileNode(pair, options);
            folderNode.Children.Add(fileNode);
        }

        // 2. Added-only bucket for right files with no left match.
        var addedPairs = pairs.Where(p => p.Left is null && p.Right is not null).ToList();
        if (addedPairs.Count > 0)
        {
            var addedRoot = new DiffNode
            {
                Kind = NodeKind.Folder,
                DisplayName = AddedOnlyFolderName,
                Key = "added-root",
                Status = DiffStatus.Added,
            };
            var addedIndex = new Dictionary<string, DiffNode>(System.StringComparer.OrdinalIgnoreCase)
            {
                [""] = addedRoot,
            };
            var emptyProjDirs = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var pair in addedPairs)
            {
                var relPath = pair.Right!.RelativePath;
                var folderRel = Path.GetDirectoryName(relPath) ?? "";
                var folderNode = EnsureFolder(folderRel.Replace('\\', '/'), addedIndex, addedRoot, emptyProjDirs);
                folderNode.Children.Add(BuildFileNode(pair, options));
            }
            root.Children.Add(addedRoot);
        }

        SortAndRollup(root);
        return root;
    }

    private static DiffNode BuildFileNode(FilePair pair, DiffOptions options)
    {
        var displayName = Path.GetFileName(
            (pair.Left?.AbsolutePath ?? pair.Right!.AbsolutePath));
        var key = pair.Left?.AbsolutePath ?? pair.Right!.AbsolutePath;

        var leftText = pair.Left is null ? null : ReadText(pair.Left.AbsolutePath);
        var rightText = pair.Right is null ? null : ReadText(pair.Right.AbsolutePath);

        var node = new DiffNode
        {
            Kind = NodeKind.File,
            DisplayName = displayName,
            Key = key,
            LeftFilePath = pair.Left?.AbsolutePath,
            RightFilePath = pair.Right?.AbsolutePath,
            LeftText = leftText,
            RightText = rightText,
        };

        if (pair.Left is null) node.Status = DiffStatus.Added;
        else if (pair.Right is null) node.Status = DiffStatus.Removed;
        else if (pair.Left.Tree is { } lt && pair.Right.Tree is { } rt)
        {
            var ln = Normalizer.Normalize(lt.GetRoot(), options);
            var rn = Normalizer.Normalize(rt.GetRoot(), options);
            node.Status = ln == rn ? DiffStatus.Unchanged : DiffStatus.Modified;
        }
        else
        {
            var ln = NormalizePlainText(leftText ?? "", options);
            var rn = NormalizePlainText(rightText ?? "", options);
            node.Status = ln == rn ? DiffStatus.Unchanged : DiffStatus.Modified;
        }

        return node;
    }

    private static string ReadText(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return ""; }
    }

    private static string NormalizePlainText(string text, DiffOptions options)
    {
        if (!options.IgnoreWhitespace) return text;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var sb = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            sb.Append(System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+", " "));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static DiffNode EnsureFolder(string relFolder, Dictionary<string, DiffNode> index, DiffNode root, HashSet<string> projectDirs)
    {
        if (index.TryGetValue(relFolder, out var existing)) return existing;

        var lastSlash = relFolder.LastIndexOf('/');
        var parentRel = lastSlash < 0 ? "" : relFolder[..lastSlash];
        var parent = EnsureFolder(parentRel, index, root, projectDirs);

        var display = lastSlash < 0 ? relFolder : relFolder[(lastSlash + 1)..];
        var kind = projectDirs.Contains(relFolder) ? NodeKind.Project : NodeKind.Folder;
        var node = new DiffNode
        {
            Kind = kind,
            DisplayName = display,
            Key = "folder:" + relFolder,
        };
        parent.Children.Add(node);
        index[relFolder] = node;
        return node;
    }

    private static DiffStatus SortAndRollup(DiffNode node)
    {
        // Folders: subfolders first, then files, both alphabetical.
        var sorted = node.Children
            .OrderBy(c => c.Kind == NodeKind.File ? 1 : 0)
            .ThenBy(c => c.DisplayName, System.StringComparer.OrdinalIgnoreCase)
            .ToList();
        node.Children.Clear();
        foreach (var c in sorted) node.Children.Add(c);

        if (node.Kind == NodeKind.File) return node.Status;

        bool anyAdded = false, anyRemoved = false, anyModified = false, anyUnchanged = false;
        foreach (var child in node.Children)
        {
            var s = SortAndRollup(child);
            switch (s)
            {
                case DiffStatus.Added: anyAdded = true; break;
                case DiffStatus.Removed: anyRemoved = true; break;
                case DiffStatus.Modified: anyModified = true; break;
                case DiffStatus.Unchanged: anyUnchanged = true; break;
            }
        }

        if (node.Status == DiffStatus.Unchanged)
        {
            if (anyModified || (anyAdded && anyRemoved) || (anyAdded && anyUnchanged) || (anyRemoved && anyUnchanged))
                node.Status = DiffStatus.Modified;
            else if (anyAdded && !anyRemoved && !anyUnchanged)
                node.Status = DiffStatus.Added;
            else if (anyRemoved && !anyAdded && !anyUnchanged)
                node.Status = DiffStatus.Removed;
        }

        return node.Status;
    }
}

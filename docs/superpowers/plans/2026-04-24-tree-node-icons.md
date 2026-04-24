# Tree Node Icons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Distinguish C# project folders (directories that contain a `.csproj`) from plain folders and files in the tree view, using unicode glyphs.

**Architecture:** Core detects project directories during the folder scan and tags matching `DiffNode`s with a new `NodeKind.Project`. The Avalonia app renders a kind-specific glyph (`📦` / `📁` / `📄`) in the tree template via a new converter.

**Tech Stack:** .NET 9, C#, Roslyn, Avalonia 12, xUnit.

---

## File Structure

**Core (`src/CsharpDiff.Core`)**
- `DiffNode.cs` — extend `NodeKind` with `Project`.
- `ProjectLoader.cs` — new `FindProjectDirectories` method returning the set of relative folder paths (scan-root-relative, `/`-separated, `""` for root) that contain a `.csproj`.
- `ProjectDiff.cs` — compute the union of left+right project dir sets, pass into folder construction, tag `NodeKind.Project` where applicable.

**App (`src/CsharpDiff.App`)**
- `Converters/NodeKindGlyphConverter.cs` — new `IValueConverter`.
- `Views/MainWindow.axaml` — register converter, extend tree template.

**Tests (`tests/CsharpDiff.Core.Tests`)**
- `ProjectDiffTests.cs` — add one fact verifying `.csproj` detection.

---

## Task 1: Extend NodeKind with Project

**Files:**
- Modify: `src/CsharpDiff.Core/DiffNode.cs`

- [ ] **Step 1: Add `Project` to the enum**

Replace the enum in `src/CsharpDiff.Core/DiffNode.cs`:

```csharp
public enum NodeKind
{
    Folder,
    Project,
    File,
}
```

- [ ] **Step 2: Build to confirm no breakage**

Run: `dotnet build csharp-diff.slnx`
Expected: build succeeds. (The existing `c.Kind == NodeKind.File ? 1 : 0` ordering keeps `Project` sorted with `Folder`, which is what we want.)

- [ ] **Step 3: Commit**

```bash
git add src/CsharpDiff.Core/DiffNode.cs
git commit -m "core: add NodeKind.Project"
```

---

## Task 2: Test-drive `.csproj` detection in ProjectDiff

**Files:**
- Test: `tests/CsharpDiff.Core.Tests/ProjectDiffTests.cs`

- [ ] **Step 1: Add the failing test**

Append this fact inside the `ProjectDiffTests` class (before the closing brace, after the last existing `[Fact]`):

```csharp
[Fact]
public void Folder_with_csproj_is_tagged_as_project()
{
    WriteLeft("ProjA/ProjA.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
    WriteLeft("ProjA/Foo.cs", "namespace N; public class Foo { }");
    WriteLeft("PlainDir/Bar.cs", "namespace N; public class Bar { }");

    WriteRight("ProjA/ProjA.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
    WriteRight("ProjA/Foo.cs", "namespace N; public class Foo { }");
    WriteRight("PlainDir/Bar.cs", "namespace N; public class Bar { }");

    var root = ProjectDiff.Compare(_left, _right);
    var projA = root.Children.FirstOrDefault(c => c.DisplayName == "ProjA");
    var plain = root.Children.FirstOrDefault(c => c.DisplayName == "PlainDir");

    Assert.NotNull(projA);
    Assert.NotNull(plain);
    Assert.Equal(NodeKind.Project, projA!.Kind);
    Assert.Equal(NodeKind.Folder, plain!.Kind);
}
```

Also add the required `using System.Linq;` at the top of the file if it is not already there.

- [ ] **Step 2: Run the test and confirm it fails**

Run: `dotnet test tests/CsharpDiff.Core.Tests/CsharpDiff.Core.Tests.csproj --filter FullyQualifiedName~Folder_with_csproj_is_tagged_as_project`
Expected: FAIL — the assertion `Assert.Equal(NodeKind.Project, projA!.Kind)` fails because `ProjA` is still tagged as `Folder`.

---

## Task 3: Detect project directories in ProjectLoader

**Files:**
- Modify: `src/CsharpDiff.Core/ProjectLoader.cs`

- [ ] **Step 1: Add `FindProjectDirectories`**

Add this method to `ProjectLoader` (below `LoadFolder`, before `EnumerateCsFiles`):

```csharp
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
```

- [ ] **Step 2: Build to confirm compilation**

Run: `dotnet build csharp-diff.slnx`
Expected: build succeeds.

---

## Task 4: Tag project folders in ProjectDiff.Compare

**Files:**
- Modify: `src/CsharpDiff.Core/ProjectDiff.cs`

- [ ] **Step 1a: Compute the union set of project directories**

In `src/CsharpDiff.Core/ProjectDiff.cs`, in `Compare`, find these two lines:

```csharp
        var leftTrees = ProjectLoader.LoadFolder(leftFolder);
        var rightTrees = ProjectLoader.LoadFolder(rightFolder);
```

Insert immediately after them:

```csharp
        var leftProjDirs = ProjectLoader.FindProjectDirectories(leftFolder);
        var rightProjDirs = ProjectLoader.FindProjectDirectories(rightFolder);
        var projectDirs = new HashSet<string>(leftProjDirs, System.StringComparer.OrdinalIgnoreCase);
        projectDirs.UnionWith(rightProjDirs);
```

- [ ] **Step 1b: Tag the root node**

Replace:

```csharp
        var root = new DiffNode
        {
            Kind = NodeKind.Folder,
            DisplayName = "<root>",
            Key = "",
        };
```

with:

```csharp
        var root = new DiffNode
        {
            Kind = projectDirs.Contains("") ? NodeKind.Project : NodeKind.Folder,
            DisplayName = "<root>",
            Key = "",
        };
```

- [ ] **Step 1c: Pass `projectDirs` to the main-tree EnsureFolder call**

In the `foreach (var pair in pairs)` loop, replace:

```csharp
            var folderNode = EnsureFolder(folderRel.Replace('\\', '/'), folderIndex, root);
```

with:

```csharp
            var folderNode = EnsureFolder(folderRel.Replace('\\', '/'), folderIndex, root, projectDirs);
```

- [ ] **Step 1d: Pass an empty set to the added-only bucket's EnsureFolder call**

Replace this entire block:

```csharp
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
            foreach (var pair in addedPairs)
            {
                var relPath = pair.Right!.RelativePath;
                var folderRel = Path.GetDirectoryName(relPath) ?? "";
                var folderNode = EnsureFolder(folderRel.Replace('\\', '/'), addedIndex, addedRoot);
                folderNode.Children.Add(BuildFileNode(pair, options));
            }
            root.Children.Add(addedRoot);
        }
```

with:

```csharp
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
```

- [ ] **Step 1e: Update `EnsureFolder`'s signature and body**

Replace the entire existing `EnsureFolder` method with:

```csharp
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
```

- [ ] **Step 2: Run the target test and confirm it passes**

Run: `dotnet test tests/CsharpDiff.Core.Tests/CsharpDiff.Core.Tests.csproj --filter FullyQualifiedName~Folder_with_csproj_is_tagged_as_project`
Expected: PASS.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test tests/CsharpDiff.Core.Tests/CsharpDiff.Core.Tests.csproj`
Expected: all tests PASS (no regressions in the existing 12 tests).

- [ ] **Step 4: Commit Core changes**

```bash
git add src/CsharpDiff.Core/DiffNode.cs src/CsharpDiff.Core/ProjectLoader.cs src/CsharpDiff.Core/ProjectDiff.cs tests/CsharpDiff.Core.Tests/ProjectDiffTests.cs
git commit -m "core: tag folders containing .csproj as NodeKind.Project"
```

---

## Task 5: Add NodeKindGlyphConverter

**Files:**
- Create: `src/CsharpDiff.App/Converters/NodeKindGlyphConverter.cs`

- [ ] **Step 1: Create the converter file**

Create `src/CsharpDiff.App/Converters/NodeKindGlyphConverter.cs` with exactly:

```csharp
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using CsharpDiff.Core;

namespace CsharpDiff.App.Converters;

public sealed class NodeKindGlyphConverter : IValueConverter
{
    public static readonly NodeKindGlyphConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is NodeKind k ? k switch
        {
            NodeKind.Project => "\U0001F4E6",
            NodeKind.Folder  => "\U0001F4C1",
            NodeKind.File    => "\U0001F4C4",
            _ => " "
        } : " ";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

(`\U0001F4E6` is 📦, `\U0001F4C1` is 📁, `\U0001F4C4` is 📄 — written as escape sequences so the source stays plain ASCII.)

- [ ] **Step 2: Build to confirm compilation**

Run: `dotnet build csharp-diff.slnx`
Expected: build succeeds.

---

## Task 6: Wire the glyph into the tree template

**Files:**
- Modify: `src/CsharpDiff.App/Views/MainWindow.axaml`

- [ ] **Step 1: Register the converter as a window resource**

In `src/CsharpDiff.App/Views/MainWindow.axaml`, change:

```xml
<Window.Resources>
    <conv:StatusGlyphConverter x:Key="StatusGlyph"/>
    <conv:StatusBrushConverter x:Key="StatusBrush"/>
</Window.Resources>
```

to:

```xml
<Window.Resources>
    <conv:StatusGlyphConverter x:Key="StatusGlyph"/>
    <conv:StatusBrushConverter x:Key="StatusBrush"/>
    <conv:NodeKindGlyphConverter x:Key="NodeKindGlyph"/>
</Window.Resources>
```

- [ ] **Step 2: Add a kind-glyph TextBlock to the tree template**

In the same file, change the `TreeDataTemplate` body from:

```xml
<TreeDataTemplate DataType="core:DiffNode" ItemsSource="{Binding Children}">
    <StackPanel Orientation="Horizontal" Spacing="6">
        <TextBlock Text="{Binding Status, Converter={StaticResource StatusGlyph}}"
                   FontFamily="Consolas,Menlo,monospace" Width="14"
                   Foreground="{Binding Status, Converter={StaticResource StatusBrush}}"/>
        <TextBlock Text="{Binding DisplayName}"
                   TextWrapping="NoWrap"
                   Foreground="{Binding Status, Converter={StaticResource StatusBrush}}"/>
    </StackPanel>
</TreeDataTemplate>
```

to:

```xml
<TreeDataTemplate DataType="core:DiffNode" ItemsSource="{Binding Children}">
    <StackPanel Orientation="Horizontal" Spacing="6">
        <TextBlock Text="{Binding Status, Converter={StaticResource StatusGlyph}}"
                   FontFamily="Consolas,Menlo,monospace" Width="14"
                   Foreground="{Binding Status, Converter={StaticResource StatusBrush}}"/>
        <TextBlock Text="{Binding Kind, Converter={StaticResource NodeKindGlyph}}"
                   Width="18"/>
        <TextBlock Text="{Binding DisplayName}"
                   TextWrapping="NoWrap"
                   Foreground="{Binding Status, Converter={StaticResource StatusBrush}}"/>
    </StackPanel>
</TreeDataTemplate>
```

- [ ] **Step 3: Build**

Run: `dotnet build csharp-diff.slnx`
Expected: build succeeds.

- [ ] **Step 4: Manual UI verification**

Run: `dotnet run --project src/CsharpDiff.App`

Pick a left and right folder that contain at least one `.csproj` in a subdirectory (for convenience, this repo itself works: pick `src/CsharpDiff.App` as one side and `src/CsharpDiff.Core` as the other). Click **Compare**.

Verify:
- Folders that contain a `.csproj` show 📦 before the name.
- Other folders show 📁.
- Individual `.cs` files show 📄.
- The status glyph (` `/`+`/`−`/`~`) is still present on the left of the kind glyph.
- Columns line up because both status and kind glyphs have fixed widths.

If the editor shows nothing or the tree is blank, close the app and rebuild (`MSB3021` guard from `CLAUDE.md`).

- [ ] **Step 5: Commit UI changes**

```bash
git add src/CsharpDiff.App/Converters/NodeKindGlyphConverter.cs src/CsharpDiff.App/Views/MainWindow.axaml
git commit -m "app: show project/folder/file glyphs in the tree"
```

---

## Done Criteria

- `NodeKind` has `Folder`, `Project`, `File`.
- `ProjectLoader.FindProjectDirectories` returns the set of relative directory paths containing a `.csproj` (case-insensitive, `/`-separated, `""` for root).
- `ProjectDiff.Compare` tags the root and any subfolder present in the union of left/right project-dir sets as `NodeKind.Project`.
- Synthetic `(new in right)` bucket and its subfolders are never tagged as projects.
- New xUnit test `Folder_with_csproj_is_tagged_as_project` passes; all existing tests still pass.
- `NodeKindGlyphConverter` produces 📦/📁/📄 for `Project`/`Folder`/`File`.
- `MainWindow` tree shows the glyph to the left of `DisplayName`.
- All changes committed in two commits: one for Core (+ test), one for App.

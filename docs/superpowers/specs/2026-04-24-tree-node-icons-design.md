# Tree Node Icons — Design

## Goal

In the folder tree, distinguish C# project folders (directories containing a `.csproj`) from plain folders and from files, using small unicode glyphs.

## Motivation

Today every tree node shows only a status glyph and a display name. When the compared root contains multiple sibling projects, the user cannot tell at a glance which nodes are project roots vs. ordinary subfolders.

## Scope

- **In:** detecting `.csproj` files during folder scan, a new `NodeKind.Project`, a new converter, a single extra glyph slot in the tree template, one test.
- **Out:** parsing `.csproj` contents, grouping files by project membership, solution (`.sln`) awareness, custom icon fonts or SVG assets.

## Changes

### Core (`CsharpDiff.Core`)

1. **`NodeKind` gains `Project`:**
   ```
   public enum NodeKind { Folder, Project, File }
   ```
2. **Project-folder detection.** During the left/right folder scan, collect the set of absolute directory paths that contain at least one `.csproj` file. This set is produced once per side and passed into the tree builder.
   - Implementation: add a helper (e.g. `ProjectLoader.FindProjectDirectories(rootFolder)` or a small static method on `ProjectDiff`) that calls `Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)` and returns the unique set of parent directories.
3. **Tag folder nodes in `ProjectDiff.Compare`.** When constructing a folder node, consult the union of the left and right project-directory sets. If the folder's absolute path on either side is in that set, set `Kind = NodeKind.Project`; otherwise `NodeKind.Folder`. The root node follows the same rule.
4. **Rollup and ordering unchanged.** `Project` nodes behave exactly like `Folder` nodes for status rollup and for the `folder-before-file` sort order in `ProjectDiff`.

### App (`CsharpDiff.App`)

1. **New converter `NodeKindGlyphConverter`** (alongside `StatusGlyphConverter`):
   - `NodeKind.Project` → `📦`
   - `NodeKind.Folder`  → `📁`
   - `NodeKind.File`    → `📄`
2. **Register the converter** as a `Window.Resources` entry in `MainWindow.axaml` (key: `NodeKindGlyph`).
3. **Extend the `TreeDataTemplate`** in `MainWindow.axaml`: add a third `TextBlock` between the status glyph and the display name, bound to `Kind` via `NodeKindGlyph`. Width and font family should match the existing status-glyph `TextBlock` so columns line up.

### Tests (`tests/CsharpDiff.Core.Tests`)

Add one test in `ProjectDiffTests`:

- Build a temp tree:
  ```
  root/
    ProjA/ProjA.csproj
    ProjA/Foo.cs
    PlainDir/Bar.cs
  ```
- Mirror this on both sides.
- Run `ProjectDiff.Compare`.
- Assert the child node named `ProjA` has `Kind == NodeKind.Project` and the child named `PlainDir` has `Kind == NodeKind.Folder`.

## Non-goals / deliberate omissions

- No caching of the project-directory set across runs — the scan is cheap enough.
- No behavior change when `.csproj` exists on only one side; the union rule already covers that case.
- No changes to `DiffStatus`, `Normalizer`, or file matching.

## Risks

- **Large trees:** `Directory.EnumerateFiles(..., AllDirectories)` walks the full tree. `ProjectLoader.LoadFolder` already walks for `*.cs`, so consider folding the `.csproj` detection into that same walk to avoid a second filesystem pass. Acceptable either way for the first cut.
- **Emoji rendering:** the three glyphs (`📦`, `📁`, `📄`) are widely supported on Windows 11; no fallback needed.

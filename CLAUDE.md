# CLAUDE.md

Context for Claude Code sessions on this repo.

## What this is

A C#-aware diff tool for comparing two folders of `.cs` files.
Built on Roslyn + Avalonia. Not a kdiff3 fork; a fresh .NET project
built around AST-level semantics.

See README.md for the user-facing story and the "Design decisions"
section for the *why* behind the architecture.

## Project layout

- `src/CsharpDiff.Core/` — Roslyn-based diff engine. No Avalonia deps.
  All comparison/matching/normalization logic lives here.
- `src/CsharpDiff.App/` — Avalonia 12 GUI. References Core.
- `tests/CsharpDiff.Core.Tests/` — xUnit tests, all exercise Core
  through `ProjectDiff.Compare`.

## Build / run / test

```bash
dotnet build csharp-diff.slnx
dotnet run --project src/CsharpDiff.App
dotnet test tests/CsharpDiff.Core.Tests/CsharpDiff.Core.Tests.csproj
```

If the build fails with `MSB3021` about a file being in use, the GUI
is still running — close it and retry.

## Architecture at a glance

```
ProjectLoader.LoadFolder(folder)
    → IReadOnlyList<SyntaxTree>
FingerprintBuilder.Build(tree, rootFolder)
    → FileFingerprint { AbsolutePath, RelativePath, Tree, Symbols }
FileMatcher.Match(left, right)
    → List<FilePair>   // 4-phase: relpath → Jaccard → filename → orphan
ProjectDiff.Compare(leftFolder, rightFolder, options)
    → DiffNode         // folder-tree mirroring left side + "(new in right)" bucket
```

The GUI calls `ProjectDiff.Compare` on a background task, binds
`DiffNode` trees to a TreeView, and loads `DiffNode.Left/RightText`
into side-by-side AvaloniaEdit panes when a file is selected.

## Conventions

- **Core has no UI dependency.** Anything touching Avalonia/DiffPlex
  belongs in App.
- **Normalization lives in Core**, called from both the equality check
  (file-level Modified/Unchanged classification) and the App display
  path (`Normalizer.NormalizeText`).
- **DiffNode is the UI-facing tree model** — `Kind`, `Status`,
  `DisplayName`, `Key`, `LeftFilePath/RightFilePath`,
  `LeftText/RightText`, `Children`. No ViewModels wrapping it.
- **No code comments unless the *why* is non-obvious.** Identifier
  names should carry the *what*.
- **xUnit + Verify-nothing style** — tests build projects in temp
  directories, run `ProjectDiff.Compare`, and assert on the resulting
  tree.

## Gotchas

- **AvaloniaEdit needs theme include**: `App.axaml` registers
  `avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml`. Without it
  the editor renders blank.
- **Compiled bindings in TreeDataTemplate**: `DataType="core:DiffNode"`
  is required so compiled bindings find `Children`, `Status`, etc.
- **TextEditor.Text vs TextEditor.Document**: assigning `Text` has
  been flaky. We assign a new `TextDocument` instead.
- **Scroll sync** uses `ScrollViewer.ScrollChanged` via a descendant
  lookup after `TemplateApplied`; the naive `TextView.ScrollOffsetChanged`
  approach didn't fire in Avalonia 12.

## When adding features

- **Matching changes** → edit `FileMatcher.Match`. Add a test covering
  the new case to `ProjectDiffTests`.
- **New ignore option** → extend `DiffOptions`, update `Normalizer`
  (both `NormalizeText` and `Normalize`), add a `[ObservableProperty]`
  on the ViewModel, wire a checkbox in `MainWindow.axaml`, add the
  property name to the `PropertyChanged` handler in
  `MainWindow.axaml.cs` so the view refreshes on toggle.
- **New node kind** → extend `NodeKind`, handle in `ProjectDiff`
  tree build, update the tree status-rollup logic.

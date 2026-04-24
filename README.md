# csharp-diff

A C#-aware diff tool for comparing two folders of `.cs` files. Built on
Roslyn + Avalonia. Matches files across folders by *what they declare*,
not by *where they live*, so renames and reorganizations don't look like
mass additions/deletions.

## Features

- **Smart file matching** — pairs files across the two folders by declared-symbol
  overlap (Jaccard similarity), so `debug/main.cs` ↔ `debug_net9/main.cs` still
  match when the contents are the same.
- **Folder tree** mirrors the left folder; right-only files land in a
  `(new in right)` bucket. Status glyphs (`+`, `−`, `~`) and colors propagate
  from file to folder.
- **Side-by-side editors** (AvaloniaEdit) with line-level diff highlighting,
  line numbers, monospace font, and two-way scroll sync.
- **Filename headers** above each editor show the full path on each side,
  colored by status (useful when paths don't match).
- **Ignore options** (toggleable at runtime, re-renders live):
  - *Ignore usings* — treat files as equivalent regardless of their `using`
    directives
  - *Ignore whitespace* — one-line vs multi-line `if`, indent changes, etc.
  - *Ignore comments* — comment-only edits don't count
  - *Changes only* — filter the tree to modified/added/removed items
  - *Normalize view* — choose whether the editors render the canonical
    normalized text or the original file with highlights suppressed
- **Recent folder pairs** persisted to `%APPDATA%/csharp-diff/recents.json`.

## Getting started

```bash
dotnet build csharp-diff.slnx
dotnet run --project src/CsharpDiff.App
dotnet test tests/CsharpDiff.Core.Tests/CsharpDiff.Core.Tests.csproj
```

Requires .NET 10 SDK.

## Project layout

```
csharp-diff.slnx
├── src/
│   ├── CsharpDiff.Core/        Roslyn-based diff engine (no UI deps)
│   │   ├── ProjectLoader.cs    folder → SyntaxTrees
│   │   ├── FileFingerprint.cs  per-file set of declared-symbol FQNs
│   │   ├── FileMatcher.cs      Jaccard matching, 4-phase
│   │   ├── Normalizer.cs       trivia/using strip + whitespace canonicalize
│   │   ├── UsingScanner.cs     using-directive line-range finder
│   │   ├── ProjectDiff.cs      top-level Compare → DiffNode tree
│   │   └── DiffNode.cs         tree model
│   └── CsharpDiff.App/         Avalonia GUI
│       ├── Views/              MainWindow.axaml(.cs)
│       ├── ViewModels/         MainWindowViewModel
│       ├── Rendering/          LineDiffer + background renderer
│       ├── Converters/         status → glyph/brush
│       └── Services/           Recents persistence
└── tests/
    └── CsharpDiff.Core.Tests/  xUnit (11 tests)
```

## Design decisions

These are the notable judgment calls made while building this, mostly
for anyone (including future me) wondering *why it works this way*.

### Roslyn, not a kdiff3 fork

The original idea was to fork kdiff3 (C++/Qt) and bolt on C#-specific
flags. Instead, we went native: Roslyn parses both sides into syntax
trees, and "equivalence" is defined at AST level. That means a one-line
`if` is automatically the same as a multi-line `if` after
`NormalizeWhitespace()` — no heuristic whitespace-regex needed.

### File-level, not symbol-level

An earlier iteration matched *types and members* across projects by
fully-qualified name and diffed each member. It worked, but users think
in files. The current design matches *files* and shows whole-file
content side-by-side, which is closer to how kdiff3 feels while still
using semantic matching instead of path matching.

### 4-phase file matching

Matching runs in priority order (higher phase wins and consumes the
file):

1. **Exact relative path** — `Services/User.cs` ↔ `Services/User.cs`
   always pairs, regardless of how much the contents diverged. This
   protects against "one file was rewritten significantly but didn't
   move" looking like a delete+add.
2. **Jaccard symbol overlap** — for each left/right candidate pair,
   score = `|L∩R| / |L∪R|` over declared type+member FQNs. Threshold
   is 0.3; ties broken by same-filename.
3. **Filename fallback** — for unmatched files, pair by filename across
   folders. Handles comment-only / attribute-only files where symbol
   overlap is zero.
4. **Orphans** — left-only → `Removed`, right-only → `Added` (bucketed
   under a virtual `(new in right)` root).

### "Ignore usings" strips, not sorts

An earlier version sorted using directives before comparison. The
current behavior is stronger: usings are removed entirely during
normalization, so two files with completely different using sets but
identical bodies compare as Unchanged. This matches how users think
about usings ("I don't care about them at all") more than sort-order
does.

### Normalize-view toggle

There are two reasonable ways to honor an ignore flag visually:

- **Normalize the view** — regenerate the displayed text from the AST
  with ignored elements removed and whitespace canonicalized. What the
  editor shows is exactly what gets diffed. Downside: the code looks
  reformatted.
- **Preserve original** — show the file as written, and suppress diff
  highlights on lines that the ignore flags say don't matter
  (using-directive lines via `UsingScanner`, whitespace-equal lines via
  DiffPlex's `ignoreWhiteSpace`). Downside: can't collapse structural
  line-count differences (one-line vs multi-line `if`).

Both have trade-offs, so it's a user-controlled checkbox: *Normalize
view* on (default) gives cleanest diffs; off preserves original
formatting.

### Tree mirrors the left folder

The left folder's directory structure is the spine of the tree.
Right-only files are grouped separately under `(new in right)` rather
than merged in — that preserves "where the file lives in the left
project" as a navigable thing. Status rolls up from files to parent
folders.

### Clipped tree, no horizontal scroll

`ScrollViewer.HorizontalScrollBarVisibility="Disabled"` on the
`TreeView`. Long namespace/file names get clipped at the right edge
instead of pushing the glyph+status marker off-screen. The left part
(where the status marker lives) is always visible.

### AvaloniaEdit needs its theme registered

For reference: AvaloniaEdit 12.0.0 requires
`<StyleInclude Source="avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml"/>`
in `Application.Styles`. Without it the `TextEditor` template never
applies and text assignments render blank — silently.

## License

[choose one — e.g. MIT]

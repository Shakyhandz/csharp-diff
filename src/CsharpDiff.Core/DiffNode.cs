using System.Collections.ObjectModel;

namespace CsharpDiff.Core;

public enum NodeKind
{
    Folder,
    Project,
    File,
}

public sealed class DiffNode
{
    public required NodeKind Kind { get; init; }
    public required string DisplayName { get; init; }
    public required string Key { get; init; }

    public DiffStatus Status { get; set; } = DiffStatus.Unchanged;

    public string? LeftFilePath { get; set; }
    public string? RightFilePath { get; set; }
    public string? LeftText { get; set; }
    public string? RightText { get; set; }

    public ObservableCollection<DiffNode> Children { get; } = new();

    public override string ToString() => $"{Kind} {DisplayName} [{Status}]";
}

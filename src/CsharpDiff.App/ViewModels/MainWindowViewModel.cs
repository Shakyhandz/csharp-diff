using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CsharpDiff.App.Services;
using CsharpDiff.Core;

namespace CsharpDiff.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private string? _leftFolder;
    [ObservableProperty] private string? _rightFolder;
    [ObservableProperty] private string? _status;
    [ObservableProperty] private bool _busy;

    [ObservableProperty] private bool _ignoreWhitespace = true;
    [ObservableProperty] private bool _ignoreComments = true;
    [ObservableProperty] private bool _changesOnly;

    private DiffNode? _selectedNode;
    public DiffNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
                SelectedNodeChanged?.Invoke(value);
        }
    }

    public event Action<DiffNode?>? SelectedNodeChanged;

    private DiffNode? _rawRoot;

    public ObservableCollection<DiffNode> RootChildren { get; } = new();

    public ObservableCollection<FolderPair> RecentPairs { get; } = new();

    public MainWindowViewModel()
    {
        ReloadRecents();
    }

    partial void OnChangesOnlyChanged(bool value) => RefreshFilteredTree();
    partial void OnIgnoreWhitespaceChanged(bool value) => _ = RecomputeIfReadyAsync();
    partial void OnIgnoreCommentsChanged(bool value) => _ = RecomputeIfReadyAsync();

    private Task RecomputeIfReadyAsync() =>
        _rawRoot is null ? Task.CompletedTask : CompareAsync();

    public async Task CompareAsync()
    {
        if (string.IsNullOrWhiteSpace(LeftFolder) || string.IsNullOrWhiteSpace(RightFolder))
        {
            Status = "Pick two folders first.";
            return;
        }

        Busy = true;
        Status = "Comparing…";
        try
        {
            var left = LeftFolder!;
            var right = RightFolder!;
            var options = new DiffOptions(
                IgnoreWhitespace: IgnoreWhitespace,
                IgnoreComments: IgnoreComments);

            _rawRoot = await Task.Run(() => ProjectDiff.Compare(left, right, options));
            RefreshFilteredTree();

            Recents.Save(new FolderPair(left, right));
            ReloadRecents();

            Status = $"Done. {_rawRoot.Children.Count} top-level folder(s)/file(s).";
        }
        catch (Exception ex)
        {
            Status = "Error: " + ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }

    public void UsePair(FolderPair pair)
    {
        LeftFolder = pair.Left;
        RightFolder = pair.Right;
    }

    private void ReloadRecents()
    {
        RecentPairs.Clear();
        foreach (var p in Recents.Load()) RecentPairs.Add(p);
    }

    private void RefreshFilteredTree()
    {
        RootChildren.Clear();
        if (_rawRoot is null) return;

        foreach (var child in _rawRoot.Children)
        {
            if (!ChangesOnly) RootChildren.Add(child);
            else if (HasChanges(child)) RootChildren.Add(Clone(child, filterUnchanged: true));
        }
    }

    private static DiffNode Clone(DiffNode source, bool filterUnchanged)
    {
        var copy = new DiffNode
        {
            Kind = source.Kind,
            DisplayName = source.DisplayName,
            Key = source.Key,
            Status = source.Status,
            LeftFilePath = source.LeftFilePath,
            RightFilePath = source.RightFilePath,
            LeftText = source.LeftText,
            RightText = source.RightText,
        };
        foreach (var c in source.Children)
        {
            if (filterUnchanged && !HasChanges(c)) continue;
            copy.Children.Add(Clone(c, filterUnchanged));
        }
        return copy;
    }

    private static bool HasChanges(DiffNode n)
    {
        if (n.Status != DiffStatus.Unchanged) return true;
        foreach (var c in n.Children)
            if (HasChanges(c)) return true;
        return false;
    }
}

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CsharpDiff.App.Rendering;
using CsharpDiff.App.Services;
using CsharpDiff.App.ViewModels;
using CsharpDiff.Core;

namespace CsharpDiff.App.Views;

public partial class MainWindow : Window
{
    private readonly DiffLineBackgroundRenderer _leftRenderer = new();
    private readonly DiffLineBackgroundRenderer _rightRenderer = new();
    private bool _syncingScroll;
    private ScrollViewer? _leftSv;
    private ScrollViewer? _rightSv;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var leftEditor = this.FindControl<TextEditor>("LeftEditor")!;
        var rightEditor = this.FindControl<TextEditor>("RightEditor")!;
        leftEditor.Options.HighlightCurrentLine = false;
        rightEditor.Options.HighlightCurrentLine = false;
        leftEditor.TextArea.TextView.BackgroundRenderers.Add(_leftRenderer);
        rightEditor.TextArea.TextView.BackgroundRenderers.Add(_rightRenderer);

        leftEditor.TemplateApplied += (_, __) => WireScrollSync(leftEditor, rightEditor);
        rightEditor.TemplateApplied += (_, __) => WireScrollSync(leftEditor, rightEditor);
        WireScrollSync(leftEditor, rightEditor);
    }

    private void WireScrollSync(TextEditor leftEditor, TextEditor rightEditor)
    {
        _leftSv ??= FindDescendant<ScrollViewer>(leftEditor);
        _rightSv ??= FindDescendant<ScrollViewer>(rightEditor);
        if (_leftSv is null || _rightSv is null) return;

        _leftSv.ScrollChanged -= OnLeftScrollChanged;
        _rightSv.ScrollChanged -= OnRightScrollChanged;
        _leftSv.ScrollChanged += OnLeftScrollChanged;
        _rightSv.ScrollChanged += OnRightScrollChanged;
    }

    private void OnLeftScrollChanged(object? sender, ScrollChangedEventArgs e) => SyncOffset(_leftSv, _rightSv);
    private void OnRightScrollChanged(object? sender, ScrollChangedEventArgs e) => SyncOffset(_rightSv, _leftSv);

    private void SyncOffset(ScrollViewer? source, ScrollViewer? target)
    {
        if (_syncingScroll || source is null || target is null) return;
        _syncingScroll = true;
        try { target.Offset = source.Offset; }
        finally { _syncingScroll = false; }
    }

    private static T? FindDescendant<T>(Visual root) where T : Visual
    {
        foreach (var child in root.GetVisualDescendants())
            if (child is T t) return t;
        return null;
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TreeView tv) return;
        var node = tv.SelectedItem as DiffNode;
        ShowNode(node);
    }

    private void ShowNode(DiffNode? node)
    {
        var leftEditor = this.FindControl<TextEditor>("LeftEditor")!;
        var rightEditor = this.FindControl<TextEditor>("RightEditor")!;
        var leftHeader = this.FindControl<TextBlock>("LeftHeader")!;
        var rightHeader = this.FindControl<TextBlock>("RightHeader")!;

        if (node is null || node.Kind != NodeKind.File)
        {
            leftEditor.Document = new TextDocument("");
            rightEditor.Document = new TextDocument("");
            leftHeader.Text = "(no selection)";
            rightHeader.Text = "(no selection)";
            leftHeader.Foreground = Brushes.Gray;
            rightHeader.Foreground = Brushes.Gray;
            _leftRenderer.SetMarks(System.Array.Empty<LineMark>());
            _rightRenderer.SetMarks(System.Array.Empty<LineMark>());
            leftEditor.TextArea.TextView.InvalidateVisual();
            rightEditor.TextArea.TextView.InvalidateVisual();
            return;
        }

        var left = node.LeftText ?? "";
        var right = node.RightText ?? "";
        leftEditor.Document = new TextDocument(left);
        rightEditor.Document = new TextDocument(right);

        leftHeader.Text = node.LeftFilePath ?? "(no match)";
        rightHeader.Text = node.RightFilePath ?? "(no match)";

        var brush = (IBrush)Converters.StatusBrushConverter.Instance.Convert(
            node.Status, typeof(IBrush), null, System.Globalization.CultureInfo.CurrentCulture);
        leftHeader.Foreground = node.LeftFilePath is null ? Brushes.Gray : brush;
        rightHeader.Foreground = node.RightFilePath is null ? Brushes.Gray : brush;

        var result = LineDiffer.Compute(left, right);
        _leftRenderer.SetMarks(result.LeftMarks);
        _rightRenderer.SetMarks(result.RightMarks);
        leftEditor.TextArea.TextView.InvalidateVisual();
        rightEditor.TextArea.TextView.InvalidateVisual();
    }

    private async void OnPickLeft(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync("Select left folder");
        if (path is not null && DataContext is MainWindowViewModel vm)
            vm.LeftFolder = path;
    }

    private async void OnPickRight(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync("Select right folder");
        if (path is not null && DataContext is MainWindowViewModel vm)
            vm.RightFolder = path;
    }

    private async void OnCompare(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            await vm.CompareAsync();
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        var v = e.Source as Visual;
        while (v is not null && v is not TreeViewItem) v = v.Parent as Visual;
        if (v is TreeViewItem tvi)
        {
            tvi.IsExpanded = !tvi.IsExpanded;
            e.Handled = true;
        }
    }

    private void OnRecentSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is FolderPair pair
            && DataContext is MainWindowViewModel vm)
        {
            vm.UsePair(pair);
            cb.SelectedItem = null;
        }
    }

    private async System.Threading.Tasks.Task<string?> PickFolderAsync(string title)
    {
        var top = GetTopLevel(this);
        if (top is null) return null;
        var result = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
        if (result.Count == 0) return null;
        return result[0].TryGetLocalPath();
    }
}

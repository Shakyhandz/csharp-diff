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
using CsharpDiff.Core;
using CsharpDiff.App.Rendering;
using CsharpDiff.App.Services;
using CsharpDiff.App.ViewModels;

namespace CsharpDiff.App.Views;

public partial class MainWindow : Window
{
    private readonly DiffLineBackgroundRenderer _leftRenderer = new();
    private readonly DiffLineBackgroundRenderer _rightRenderer = new();
    private readonly GapAwareLineNumberMargin _leftMargin = new();
    private readonly GapAwareLineNumberMargin _rightMargin = new();
    private bool _syncingScroll;
    private ScrollViewer? _leftSv;
    private ScrollViewer? _rightSv;

    private DiffNode? _currentNode;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        DataContextChanged += (_, __) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(vm.IgnoreUsings)
                        or nameof(vm.IgnoreWhitespace)
                        or nameof(vm.IgnoreComments)
                        or nameof(vm.NormalizeView))
                    {
                        ShowNode(_currentNode);
                    }
                };
            }
        };
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var leftEditor = this.FindControl<TextEditor>("LeftEditor")!;
        var rightEditor = this.FindControl<TextEditor>("RightEditor")!;
        leftEditor.Options.HighlightCurrentLine = false;
        rightEditor.Options.HighlightCurrentLine = false;
        leftEditor.TextArea.TextView.BackgroundRenderers.Add(_leftRenderer);
        rightEditor.TextArea.TextView.BackgroundRenderers.Add(_rightRenderer);
        leftEditor.TextArea.LeftMargins.Insert(0, _leftMargin);
        rightEditor.TextArea.LeftMargins.Insert(0, _rightMargin);

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
        _currentNode = tv.SelectedItem as DiffNode;
        ShowNode(_currentNode);
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
            _leftMargin.SetMap(System.Array.Empty<int>());
            _rightMargin.SetMap(System.Array.Empty<int>());
            leftEditor.TextArea.TextView.InvalidateVisual();
            rightEditor.TextArea.TextView.InvalidateVisual();
            return;
        }

        var vm = DataContext as MainWindowViewModel;
        var options = new DiffOptions(
            IgnoreUsings: vm?.IgnoreUsings ?? false,
            IgnoreWhitespace: vm?.IgnoreWhitespace ?? false,
            IgnoreComments: vm?.IgnoreComments ?? false);
        var normalizeView = vm?.NormalizeView ?? true;

        string left, right;
        if (normalizeView)
        {
            // Display-canonical form — editor and diff both use normalized text.
            left = Normalizer.NormalizeText(node.LeftText ?? "", options);
            right = Normalizer.NormalizeText(node.RightText ?? "", options);
        }
        else
        {
            // Preserve original file text; highlight suppression handled below.
            left = node.LeftText ?? "";
            right = node.RightText ?? "";
        }

        var result = normalizeView
            ? LineDiffer.Compute(left, right)
            : LineDiffer.Compute(left, right,
                ignoreUsings: options.IgnoreUsings,
                ignoreWhitespace: options.IgnoreWhitespace);

        leftEditor.Document = new TextDocument(result.LeftAligned);
        rightEditor.Document = new TextDocument(result.RightAligned);

        leftHeader.Text = node.LeftFilePath ?? "(no match)";
        rightHeader.Text = node.RightFilePath ?? "(no match)";

        var brush = (IBrush)Converters.StatusBrushConverter.Instance.Convert(
            node.Status, typeof(IBrush), null, System.Globalization.CultureInfo.CurrentCulture);
        leftHeader.Foreground = node.LeftFilePath is null ? Brushes.Gray : brush;
        rightHeader.Foreground = node.RightFilePath is null ? Brushes.Gray : brush;

        _leftRenderer.SetMarks(result.LeftMarks);
        _rightRenderer.SetMarks(result.RightMarks);
        _leftMargin.SetMap(result.LeftLineNumbers);
        _rightMargin.SetMap(result.RightLineNumbers);
        leftEditor.TextArea.TextView.InvalidateVisual();
        rightEditor.TextArea.TextView.InvalidateVisual();

        var firstDiff = FindFirstDiffLine(result.LeftMarks, result.RightMarks);

        var scrollTo = firstDiff > 3 
                       ? firstDiff - 2 
                       : 1;

        leftEditor.ScrollToLine(scrollTo);
        rightEditor.ScrollToLine(scrollTo);
    }

    private static int FindFirstDiffLine(System.Collections.Generic.IReadOnlyList<LineMark> left, System.Collections.Generic.IReadOnlyList<LineMark> right)
    {
        var max = System.Math.Max(left.Count, right.Count);
        for (var i = 0; i < max; i++)
        {
            var l = i < left.Count ? left[i] : LineMark.None;
            var r = i < right.Count ? right[i] : LineMark.None;
            if (l != LineMark.None || r != LineMark.None) return i + 1;
        }
        return 0;
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

    private async void OnRecentSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is FolderPair pair
            && DataContext is MainWindowViewModel vm)
        {
            vm.UsePair(pair);
            cb.SelectedItem = null;
            await vm.CompareAsync();
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

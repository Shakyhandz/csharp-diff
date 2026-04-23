using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CsharpDiff.Core;

namespace CsharpDiff.App.Converters;

public sealed class StatusBrushConverter : IValueConverter
{
    public static readonly StatusBrushConverter Instance = new();

    private static readonly IBrush AddedBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x30));
    private static readonly IBrush RemovedBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x20, 0x20));
    private static readonly IBrush ModifiedBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x80, 0x00));
    private static readonly IBrush DefaultBrush = Brushes.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DiffStatus s ? s switch
        {
            DiffStatus.Added => AddedBrush,
            DiffStatus.Removed => RemovedBrush,
            DiffStatus.Modified => ModifiedBrush,
            _ => DefaultBrush,
        } : DefaultBrush;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

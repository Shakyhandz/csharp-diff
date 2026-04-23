using System;
using System.Globalization;
using Avalonia.Data.Converters;
using CsharpDiff.Core;

namespace CsharpDiff.App.Converters;

public sealed class StatusGlyphConverter : IValueConverter
{
    public static readonly StatusGlyphConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DiffStatus s ? s switch
        {
            DiffStatus.Added => "+",
            DiffStatus.Removed => "−",
            DiffStatus.Modified => "~",
            _ => " "
        } : " ";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

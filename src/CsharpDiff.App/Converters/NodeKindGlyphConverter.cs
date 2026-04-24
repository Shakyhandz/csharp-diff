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

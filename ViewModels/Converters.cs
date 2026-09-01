using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace IsSubtitled.ViewModels;

public static class Converters
{
    /// <summary>Expanded/collapsed chevron for a result group header.</summary>
    public static readonly IValueConverter ChevronConverter =
        new FuncValueConverter<bool, string>(expanded => expanded ? "▾" : "▸");
}

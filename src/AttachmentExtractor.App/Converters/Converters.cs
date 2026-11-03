using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.App.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool boolean && boolean;
        if (parameter is string text &&
            text.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is not null;
        if (parameter is string text &&
            text.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not bool boolean || !boolean;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not bool boolean || !boolean;
    }
}

public sealed class LogLevelToBrushConverter : IValueConverter
{
    private static readonly Brush InfoBrush = CreateBrush("#475569");
    private static readonly Brush SuccessBrush = CreateBrush("#047857");
    private static readonly Brush WarningBrush = CreateBrush("#B45309");
    private static readonly Brush ErrorBrush = CreateBrush("#B91C1C");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is LogLevel level
            ? level switch
            {
                LogLevel.Success => SuccessBrush,
                LogLevel.Warning => WarningBrush,
                LogLevel.Error => ErrorBrush,
                _ => InfoBrush
            }
            : InfoBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}

public sealed class ExtensionToBrushConverter : IValueConverter
{
    private static readonly Brush PdfBrush = CreateBrush("#B91C1C");
    private static readonly Brush WordBrush = CreateBrush("#1D4ED8");
    private static readonly Brush ExcelBrush = CreateBrush("#047857");
    private static readonly Brush PowerpointBrush = CreateBrush("#C2410C");
    private static readonly Brush ImageBrush = CreateBrush("#6D28D9");
    private static readonly Brush ArchiveBrush = CreateBrush("#92400E");
    private static readonly Brush TextBrush = CreateBrush("#475569");
    private static readonly Brush DefaultBrush = CreateBrush("#64748B");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var extension = value?.ToString()?.Trim().TrimStart('.').ToLowerInvariant() ?? string.Empty;
        return extension switch
        {
            "pdf" => PdfBrush,
            "doc" or "docx" or "rtf" or "odt" => WordBrush,
            "xls" or "xlsx" or "csv" or "ods" => ExcelBrush,
            "ppt" or "pptx" or "odp" => PowerpointBrush,
            "jpg" or "jpeg" or "png" or "gif" or "bmp" or "tif" or "tiff" or "webp" or "svg" => ImageBrush,
            "zip" or "rar" or "7z" or "tar" or "gz" => ArchiveBrush,
            "txt" or "log" or "xml" or "json" or "htm" or "html" => TextBrush,
            _ => DefaultBrush
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}

public sealed class ImportanceToBrushConverter : IValueConverter
{
    private static readonly Brush HighBrush = CreateBrush("#B91C1C");
    private static readonly Brush NormalBrush = CreateBrush("#64748B");
    private static readonly Brush LowBrush = CreateBrush("#1D4ED8");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int importance
            ? importance switch
            {
                2 => HighBrush,
                0 => LowBrush,
                _ => NormalBrush
            }
            : NormalBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}

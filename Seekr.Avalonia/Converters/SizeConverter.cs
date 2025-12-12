using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Seekr.Services;

namespace Seekr.Avalonia.Converters;

public class SizeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long size)
        {
            return FormatSize(size);
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private string FormatSize(double bytes)
    {
        var sizeUnit = SettingsService.Settings?.SizeUnit ?? "Auto";
        
        if (sizeUnit == "Auto")
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            while (bytes >= 1024 && order < sizes.Length - 1)
            {
                order++;
                bytes /= 1024;
            }
            return $"{bytes:0.##} {sizes[order]}";
        }
        
        // Fixed unit conversion
        return sizeUnit switch
        {
            "Bytes" => $"{bytes:N0} B",
            "KB" => $"{bytes / 1024.0:N2} KB",
            "MB" => $"{bytes / (1024.0 * 1024.0):N2} MB",
            "GB" => $"{bytes / (1024.0 * 1024.0 * 1024.0):N2} GB",
            _ => $"{bytes:N0} B"
        };
    }
}

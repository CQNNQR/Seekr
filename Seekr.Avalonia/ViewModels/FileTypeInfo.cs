namespace Seekr.Avalonia.ViewModels;

/// <summary>
/// Information about a file type category
/// </summary>
public class FileTypeInfo
{
    public string Extension { get; set; } = string.Empty;
    public int Count { get; set; }
    public long TotalSize { get; set; }
    public double Percentage { get; set; }

    public string FormattedSize => FormatSize(TotalSize);
    public string PercentageFormatted => $"{Percentage:0.#}%";

    public static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

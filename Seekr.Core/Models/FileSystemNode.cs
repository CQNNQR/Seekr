using Seekr.Services;

namespace Seekr.Models;

/// <summary>
/// Represents a file or folder in the file system with size and hierarchy information
/// </summary>
public class FileSystemNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long Size { get; set; }
    public int ItemCount { get; set; }
    public bool IsDirectory { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreationTime { get; set; }
    public List<FileSystemNode> Children { get; set; } = new();
    public FileSystemNode? Parent { get; set; }
    // Optional error info captured when scanning this node (access denied, etc.)
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    private long? _totalSize;
    /// <summary>
    /// Gets the total size including all children
    /// </summary>
    public long TotalSize 
    {
        get 
        {
            if (!_totalSize.HasValue)
            {
                _totalSize = Size + Children.Sum(c => c.TotalSize);
            }
            return _totalSize.Value;
        }
        set => _totalSize = value;
    }

    private int? _totalItemCount;
    /// <summary>
    /// Gets the total item count including all children
    /// </summary>
    public int TotalItemCount 
    {
        get 
        {
            if (!_totalItemCount.HasValue)
            {
                _totalItemCount = ItemCount + Children.Sum(c => c.TotalItemCount);
            }
            return _totalItemCount.Value;
        }
        set => _totalItemCount = value;
    }

    /// <summary>
    /// Formats the size in human-readable format
    /// </summary>
    public string FormattedSize => FormatBytes(TotalSize);

    /// <summary>
    /// Formats bytes into human-readable string
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        var unit = SettingsService.Settings?.SizeUnit ?? "Auto";
        
        if (unit == "Bytes") return $"{bytes:N0} B";
        if (unit == "KB") return $"{bytes / 1024.0:N2} KB";
        if (unit == "MB") return $"{bytes / (1024.0 * 1024.0):N2} MB";
        if (unit == "GB") return $"{bytes / (1024.0 * 1024.0 * 1024.0):N2} GB";

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public override string ToString()
    {
        var baseText = IsDirectory
            ? $"{Name} ({FormattedSize}, {TotalItemCount} items)"
            : $"{Name} ({FormattedSize})";

        if (HasError)
        {
            var err = string.IsNullOrEmpty(ErrorMessage) ? "(scan error)" : ErrorMessage;
            return $"{baseText} [ERROR: {err}]";
        }

        return baseText;
    }
}

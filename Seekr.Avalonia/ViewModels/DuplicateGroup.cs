using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using Seekr.Models;

namespace Seekr.Avalonia.ViewModels;

/// <summary>
/// Represents a group of duplicate files with the same content
/// </summary>
public class DuplicateGroup : ReactiveObject
{
    public long Size { get; set; }
    public List<FileSystemNode> Files { get; set; } = new();

    private string _hash = string.Empty;
    public string Hash
    {
        get => _hash;
        set => this.RaiseAndSetIfChanged(ref _hash, value);
    }

    private bool _isVerified;
    public bool IsVerified
    {
        get => _isVerified;
        set => this.RaiseAndSetIfChanged(ref _isVerified, value);
    }

    public int Count => Files.Count;
    public long WastedSpace => Size * (Count - 1);

    public string FormattedSize => FileTypeInfo.FormatSize(Size);
    public string WastedSpaceFormatted => FileTypeInfo.FormatSize(WastedSpace);
    public string StatusIcon => IsVerified ? "✓" : "?";
    public string FileNames => string.Join(", ", Files.Take(5).Select(f => f.Name)) + (Files.Count > 5 ? $" (+{Files.Count - 5} more)" : "");
}

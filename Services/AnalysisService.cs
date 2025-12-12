using Seekr.Models;
using Serilog;

namespace Seekr.Services;

/// <summary>
/// Service for analyzing scanned file system data and generating statistics
/// </summary>
public class AnalysisService
{
    /// <summary>
    /// Analyzes file types and their distribution
    /// </summary>
    public Dictionary<string, long> GetFileTypeDistribution(FileSystemNode rootNode)
    {
        Log.Verbose("Computing file type distribution for {RootPath}", rootNode.FullPath);
        var distribution = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        CollectFileTypes(rootNode, distribution);
        return distribution;
    }

    /// <summary>
    /// Gets the top N largest items (files and directories) from the immediate children of the root node
    /// </summary>
    public List<FileSystemNode> GetLargestItems(FileSystemNode rootNode, int count = 100)
    {
        // For charts, we usually want to see what's taking up space in the *current* folder,
        // not a flat list of all files deep down.
        return rootNode.Children
            .OrderByDescending(c => c.TotalSize)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Gets the top N largest files from the scan
    /// </summary>
    public List<FileSystemNode> GetTopFiles(FileSystemNode rootNode, int count = 100)
    {
        Log.Verbose("Finding top {Count} files under {RootPath}", count, rootNode.FullPath);
        var allFiles = new List<FileSystemNode>();
        CollectAllFiles(rootNode, allFiles);
        
        return allFiles
            .OrderByDescending(f => f.Size)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Gets the top N largest directories from the scan
    /// </summary>
    public List<FileSystemNode> GetTopDirectories(FileSystemNode rootNode, int count = 100)
    {
        Log.Verbose("Finding top {Count} directories under {RootPath}", count, rootNode.FullPath);
        var allDirectories = new List<FileSystemNode>();
        CollectAllDirectories(rootNode, allDirectories);
        
        return allDirectories
            .OrderByDescending(d => d.TotalSize)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Categorizes file extensions into logical groups
    /// </summary>
    public Dictionary<string, long> GetCategorizedDistribution(FileSystemNode rootNode)
    {
        Log.Verbose("Categorizing file extensions for {RootPath}", rootNode.FullPath);
        var fileTypes = GetFileTypeDistribution(rootNode);
        var categories = new Dictionary<string, long>();

        foreach (var (extension, size) in fileTypes)
        {
            var category = CategorizeExtension(extension);
            categories[category] = categories.GetValueOrDefault(category, 0) + size;
        }

        return categories;
    }

    /// <summary>
    /// Gets summary statistics for the scan
    /// </summary>
    public ScanSummary GetScanSummary(FileSystemNode rootNode)
    {
        Log.Verbose("Computing scan summary for {RootPath}", rootNode.FullPath);
        var allFiles = new List<FileSystemNode>();
        var allDirectories = new List<FileSystemNode>();
        
        CollectAllFiles(rootNode, allFiles);
        CollectAllDirectories(rootNode, allDirectories);

        var summary = new ScanSummary
        {
            TotalSize = rootNode.TotalSize,
            TotalFiles = allFiles.Count,
            TotalDirectories = allDirectories.Count,
            LargestFile = allFiles.MaxBy(f => f.Size),
            LargestDirectory = allDirectories.MaxBy(d => d.TotalSize),
            AverageFileSize = allFiles.Any() 
                ? (long)allFiles.Average(f => f.Size)
                : 0
        };

        Log.Debug("ScanSummary: TotalSize={TotalSize} TotalFiles={TotalFiles} TotalDirectories={TotalDirectories}",
            summary.TotalSize, summary.TotalFiles, summary.TotalDirectories);

        return summary;
    }

    private void CollectFileTypes(FileSystemNode node, Dictionary<string, long> distribution)
    {
        if (!node.IsDirectory)
        {
            var extension = Path.GetExtension(node.Name).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension))
                extension = "(no extension)";
            
            distribution[extension] = distribution.GetValueOrDefault(extension, 0) + node.Size;
        }

        foreach (var child in node.Children)
        {
            CollectFileTypes(child, distribution);
        }
    }

    private void CollectAllFiles(FileSystemNode node, List<FileSystemNode> files)
    {
        if (!node.IsDirectory)
        {
            files.Add(node);
        }
        
        foreach (var child in node.Children)
        {
            CollectAllFiles(child, files);
        }
    }

    private void CollectAllDirectories(FileSystemNode node, List<FileSystemNode> directories)
    {
        if (node.IsDirectory)
        {
            directories.Add(node);
        }
        
        foreach (var child in node.Children)
        {
            CollectAllDirectories(child, directories);
        }
    }

    private string CategorizeExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tiff" or ".webp" => "Images",
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" => "Videos",
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" => "Audio",
            ".txt" or ".doc" or ".docx" or ".pdf" or ".rtf" or ".odt" => "Documents",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" => "Archives",
            ".exe" or ".msi" or ".dll" or ".sys" or ".com" => "Executables",
            ".cs" or ".cpp" or ".c" or ".h" or ".java" or ".py" or ".js" or ".html" or ".css" => "Code",
            "(no extension)" => "No Extension",
            _ => "Other"
        };
    }
}

/// <summary>
/// Summary statistics for a scan operation
/// </summary>
public class ScanSummary
{
    public long TotalSize { get; set; }
    public int TotalFiles { get; set; }
    public int TotalDirectories { get; set; }
    public FileSystemNode? LargestFile { get; set; }
    public FileSystemNode? LargestDirectory { get; set; }
    public long AverageFileSize { get; set; }
}

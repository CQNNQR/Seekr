namespace Seekr.Models;

/// <summary>
/// Configuration options for disk scanning operations
/// </summary>
public class ScanOptions
{
    public bool ScanHiddenFiles { get; set; } = true;
    public bool ScanSystemFiles { get; set; } = true;
    public int MaxDepth { get; set; } = -1; // -1 for unlimited
    public bool FollowSymlinks { get; set; } = false;
    // Retry behavior for removable or lazy-mounted drives that may not be ready immediately
    public bool RetryOnDriveNotReady { get; set; } = true;
    public int RetryCount { get; set; } = 5;
    public int RetryDelayMs { get; set; } = 1000;
    public string[] ExcludedExtensions { get; set; } = Array.Empty<string>();
    public string[] ExcludedFolders { get; set; } = Array.Empty<string>();
    
    public static ScanOptions Default => new();
}

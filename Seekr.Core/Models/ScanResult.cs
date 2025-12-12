namespace Seekr.Models;

/// <summary>
/// Holds the result of a scan operation including status flags and error messages
/// </summary>
public class ScanResult
{
    public FileSystemNode? Root { get; set; }
    public bool IsCancelled { get; set; }
    public string? ErrorMessage { get; set; }

    public bool IsSuccessful => Root != null && !IsCancelled && string.IsNullOrEmpty(ErrorMessage);
}
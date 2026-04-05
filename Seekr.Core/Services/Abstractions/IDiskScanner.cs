using Seekr.Models;
using Seekr.Services;

namespace Seekr.Core.Services.Abstractions;

/// <summary>
/// Interface for disk scanning operations
/// </summary>
public interface IDiskScanner
{
    event EventHandler<ScanProgressEventArgs>? ProgressChanged;
    event EventHandler<string>? CurrentDirectoryChanged;

    Task<ScanResult> ScanAsync(string rootPath, IProgress<int>? progress = null);
    void Cancel();
}
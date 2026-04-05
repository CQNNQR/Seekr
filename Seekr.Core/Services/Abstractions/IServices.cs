using Seekr.Models;

namespace Seekr.Core.Services.Abstractions;

/// <summary>
/// Interface for settings management
/// </summary>
public interface ISettingsService
{
    AppSettings Settings { get; }
    bool LoadFailed { get; }
    void Load();
    void Save();
}

/// <summary>
/// Interface for update operations
/// </summary>
public interface IUpdateService
{
    string CurrentVersion { get; }
    string LastErrorMessage { get; }
    Task<UpdateInfo> CheckForUpdatesAsync();
    Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, IProgress<DownloadProgress>? progress = null);
    void OpenReleasesPage();
    string FormatFileSize(long bytes);
}

/// <summary>
/// Information about an available update
/// </summary>
public class UpdateInfo
{
    public bool IsUpdateAvailable { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleasePageUrl { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public long FileSizeBytes { get; set; }
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// Progress information during download
/// </summary>
public class DownloadProgress
{
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public double PercentComplete => TotalBytes > 0 ? (BytesDownloaded * 100.0 / TotalBytes) : 0;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Interface for telemetry operations
/// </summary>
public interface ITelemetryService
{
    bool IsEnabled { get; set; }
    string SessionId { get; }
    string AppVersion { get; }
    string Platform { get; }
    Task TrackAppLaunchAsync();
    Task TrackScanCompletedAsync(long totalSizeBytes, long filesScanned);
    Task TrackDuplicateScanAsync(int duplicateGroupsFound, long potentialSavingsBytes);
}
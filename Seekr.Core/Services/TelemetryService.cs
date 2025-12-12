using System.Net.Http;
using System.Text;
using System.Text.Json;
using Serilog;

namespace Seekr.Services;

/// <summary>
/// Lightweight, privacy-respecting telemetry service.
/// Sends anonymous usage statistics to help improve Seekr.
/// All data collection is opt-in and can be disabled in settings.
/// </summary>
public static class TelemetryService
{
    // Google Form URL for telemetry
    private const string TelemetryEndpoint = "https://docs.google.com/forms/d/e/1FAIpQLSfN1w0kJJ1OqYLNq84DU-WQzMQO0g0ZknrpGXQv1_-GGbJb0A/formResponse";
    
    // Google Form field IDs (from form inspection) - order matches form questions
    private const string FieldAppVersion = "entry.1288398803";   // App Version
    private const string FieldPlatform = "entry.1684401751";     // Platform
    private const string FieldScanSizeMB = "entry.817773117";    // Scan Size MB
    private const string FieldEvent = "entry.651953315";         // Event
    private const string FieldFilesScanned = "entry.1171577566"; // Files Scanned
    private const string FieldSessionId = "entry.1544516264";    // Session ID
    private const string FieldTimestamp = "entry.1898132806";    // Timestamp
    
    private static readonly HttpClient _httpClient = new() 
    { 
        Timeout = TimeSpan.FromSeconds(5) 
    };
    
    private static string? _sessionId;
    private static bool _isEnabled = true;
    
    /// <summary>
    /// Unique session ID (regenerated each app launch, not persistent)
    /// </summary>
    public static string SessionId => _sessionId ??= Guid.NewGuid().ToString("N")[..12];
    
    /// <summary>
    /// Current app version
    /// </summary>
    public static string AppVersion => "1.0.0";
    
    /// <summary>
    /// Current platform (Windows, Linux, macOS)
    /// </summary>
    public static string Platform
    {
        get
        {
            if (OperatingSystem.IsWindows()) return "Windows";
            if (OperatingSystem.IsLinux()) return "Linux";
            if (OperatingSystem.IsMacOS()) return "macOS";
            return "Unknown";
        }
    }
    
    /// <summary>
    /// Enable or disable telemetry
    /// </summary>
    public static bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            Log.Information("Telemetry {Status}", value ? "enabled" : "disabled");
        }
    }
    
    /// <summary>
    /// Track app launch event
    /// </summary>
    public static Task TrackAppLaunchAsync()
    {
        return SendEventAsync("app_launch");
    }
    
    /// <summary>
    /// Track scan completed event with statistics
    /// </summary>
    public static Task TrackScanCompletedAsync(long totalSizeBytes, long filesScanned)
    {
        var sizeMB = totalSizeBytes / (1024.0 * 1024.0);
        return SendEventAsync("scan_complete", sizeMB, filesScanned);
    }
    
    /// <summary>
    /// Track duplicate scan event
    /// </summary>
    public static Task TrackDuplicateScanAsync(int duplicateGroupsFound, long potentialSavingsBytes)
    {
        var savingsMB = potentialSavingsBytes / (1024.0 * 1024.0);
        return SendEventAsync("duplicate_scan", savingsMB, duplicateGroupsFound);
    }
    
    /// <summary>
    /// Track file deletion event (count only, no paths)
    /// </summary>
    public static Task TrackFilesDeletedAsync(int count, long totalSizeBytes)
    {
        var sizeMB = totalSizeBytes / (1024.0 * 1024.0);
        return SendEventAsync("files_deleted", sizeMB, count);
    }
    
    /// <summary>
    /// Send event to telemetry endpoint
    /// </summary>
    private static async Task SendEventAsync(string eventName, double sizeMB = 0, long itemCount = 0)
    {
        if (!IsEnabled)
        {
            Log.Debug("Telemetry disabled, skipping event: {Event}", eventName);
            return;
        }
        
        try
        {
            // Build form data for Google Forms
            var formData = new Dictionary<string, string>
            {
                [FieldAppVersion] = AppVersion,
                [FieldPlatform] = Platform,
                [FieldEvent] = eventName,
                [FieldScanSizeMB] = sizeMB.ToString("F2"),
                [FieldFilesScanned] = itemCount.ToString(),
                [FieldSessionId] = SessionId,
                [FieldTimestamp] = DateTime.UtcNow.ToString("O")
            };
            
            var content = new FormUrlEncodedContent(formData);
            
            // Fire and forget - don't wait for response, don't block the app
            _ = Task.Run(async () =>
            {
                try
                {
                    var response = await _httpClient.PostAsync(TelemetryEndpoint, content);
                    Log.Debug("Telemetry event '{Event}' sent: {Status}", eventName, response.StatusCode);
                }
                catch (Exception ex)
                {
                    // Silently fail - telemetry should never impact user experience
                    Log.Debug("Telemetry send failed (this is OK): {Error}", ex.Message);
                }
            });
        }
        catch (Exception ex)
        {
            Log.Debug("Telemetry preparation failed: {Error}", ex.Message);
        }
    }
}

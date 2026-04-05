using System.Net.Http;
using System.Text;
using System.Text.Json;
using Seekr.Core.Services.Abstractions;
using Serilog;

namespace Seekr.Services;

/// <summary>
/// Lightweight, privacy-respecting telemetry service.
/// Sends anonymous usage statistics to help improve Seekr.
/// All data collection is opt-in and can be disabled in settings.
/// </summary>
public class TelemetryServiceImpl : ITelemetryService
{
    private const string TelemetryEndpoint = "https://docs.google.com/forms/d/e/1FAIpQLSfN1w0kJJ1OqYLNq84DU-WQzMQO0g0ZknrpGXQv1_-GGbJb0A/formResponse";

    private const string FieldAppVersion = "entry.1288398803";
    private const string FieldPlatform = "entry.1684401751";
    private const string FieldScanSizeMB = "entry.817773117";
    private const string FieldEvent = "entry.651953315";
    private const string FieldFilesScanned = "entry.1171577566";
    private const string FieldSessionId = "entry.1544516264";
    private const string FieldTimestamp = "entry.1898132806";

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private string? _sessionId;
    private bool _isEnabled = true;

    public string SessionId => _sessionId ??= Guid.NewGuid().ToString("N")[..12];

    public string AppVersion => "1.0.0";

    public string Platform
    {
        get
        {
            if (OperatingSystem.IsWindows()) return "Windows";
            if (OperatingSystem.IsLinux()) return "Linux";
            if (OperatingSystem.IsMacOS()) return "macOS";
            return "Unknown";
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            Log.Information("Telemetry {Status}", value ? "enabled" : "disabled");
        }
    }

    public Task TrackAppLaunchAsync()
    {
        return SendEventAsync("app_launch");
    }

    public Task TrackScanCompletedAsync(long totalSizeBytes, long filesScanned)
    {
        var sizeMB = totalSizeBytes / (1024.0 * 1024.0);
        return SendEventAsync("scan_complete", sizeMB, filesScanned);
    }

    public Task TrackDuplicateScanAsync(int duplicateGroupsFound, long potentialSavingsBytes)
    {
        var savingsMB = potentialSavingsBytes / (1024.0 * 1024.0);
        return SendEventAsync("duplicate_scan", savingsMB, duplicateGroupsFound);
    }

    private async Task SendEventAsync(string eventName, double sizeMB = 0, long itemCount = 0)
    {
        if (!IsEnabled)
        {
            Log.Debug("Telemetry disabled, skipping event: {Event}", eventName);
            return;
        }

        try
        {
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

            _ = Task.Run(async () =>
            {
                try
                {
                    var response = await _httpClient.PostAsync(TelemetryEndpoint, content);
                    Log.Debug("Telemetry event '{Event}' sent: {Status}", eventName, response.StatusCode);
                }
                catch (Exception ex)
                {
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
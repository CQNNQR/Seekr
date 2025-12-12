using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using Serilog;

namespace Seekr.Services;

/// <summary>
/// Service to check for app updates via GitHub Releases API and perform in-app updates.
/// Completely free, uses public GitHub API (no auth required for public repos).
/// </summary>
public static class UpdateService
{
    private const string GitHubOwner = "CQNNQR";
    private const string GitHubRepo = "Seekr";
    private const string ReleasesApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
    private const string ReleasesPageUrl = $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest";
    
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    
    static UpdateService()
    {
        // GitHub API requires a User-Agent header
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"Seekr/{CurrentVersion}");
    }
    
    /// <summary>
    /// Current app version
    /// </summary>
    public static string CurrentVersion => "1.0.1";
    
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
    /// Check GitHub Releases for a newer version
    /// </summary>
    public static async Task<UpdateInfo> CheckForUpdatesAsync()
    {
        var result = new UpdateInfo
        {
            IsUpdateAvailable = false,
            ReleasePageUrl = ReleasesPageUrl
        };
        
        try
        {
            Log.Information("Checking for updates at {Url}", ReleasesApiUrl);
            
            var response = await _httpClient.GetAsync(ReleasesApiUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Update check failed: {StatusCode}", response.StatusCode);
                return result;
            }
            
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            // Parse version from tag_name (e.g., "v1.0.1" or "1.0.1")
            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var latestVersion = tagName.TrimStart('v', 'V');
            
            result.LatestVersion = latestVersion;
            result.ReleaseNotes = root.GetProperty("body").GetString() ?? "";
            result.ReleasePageUrl = root.GetProperty("html_url").GetString() ?? ReleasesPageUrl;
            
            if (root.TryGetProperty("published_at", out var publishedAt))
            {
                result.PublishedAt = DateTime.Parse(publishedAt.GetString() ?? "");
            }
            
            // Find download URL for current platform
            if (root.TryGetProperty("assets", out var assets))
            {
                var platformSuffix = GetPlatformSuffix();
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.Contains(platformSuffix, StringComparison.OrdinalIgnoreCase) &&
                        (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || 
                         name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                         !name.Contains(".")))
                    {
                        result.DownloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                        result.FileName = name;
                        if (asset.TryGetProperty("size", out var size))
                        {
                            result.FileSizeBytes = size.GetInt64();
                        }
                        break;
                    }
                }
            }
            
            // Compare versions
            result.IsUpdateAvailable = IsNewerVersion(latestVersion, CurrentVersion);
            
            Log.Information("Update check complete. Current: {Current}, Latest: {Latest}, Update available: {Available}",
                CurrentVersion, latestVersion, result.IsUpdateAvailable);
            
            return result;
        }
        catch (Exception ex)
        {
            Log.Warning("Update check failed: {Error}", ex.Message);
            return result;
        }
    }
    
    /// <summary>
    /// Download and install the update, preserving settings and logs
    /// </summary>
    public static async Task<bool> DownloadAndInstallUpdateAsync(
        UpdateInfo updateInfo, 
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
        {
            Log.Error("No download URL available for update");
            return false;
        }
        
        var appDir = AppContext.BaseDirectory;
        var tempDir = Path.Combine(Path.GetTempPath(), $"Seekr_Update_{Guid.NewGuid():N}");
        var downloadPath = Path.Combine(tempDir, updateInfo.FileName);
        
        try
        {
            Directory.CreateDirectory(tempDir);
            
            // Step 1: Download the update
            progress?.Report(new DownloadProgress { Status = "Downloading update..." });
            Log.Information("Downloading update from {Url}", updateInfo.DownloadUrl);
            
            using (var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, 
                HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? updateInfo.FileSizeBytes;
                
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);
                
                var buffer = new byte[81920]; // 80KB buffer
                long bytesDownloaded = 0;
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    bytesDownloaded += bytesRead;
                    
                    progress?.Report(new DownloadProgress
                    {
                        BytesDownloaded = bytesDownloaded,
                        TotalBytes = totalBytes,
                        Status = $"Downloading... {bytesDownloaded / 1024 / 1024:F1} MB / {totalBytes / 1024 / 1024:F1} MB"
                    });
                }
            }
            
            progress?.Report(new DownloadProgress { Status = "Preparing update..." });
            
            // Step 2: Extract if it's a zip file
            var extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractDir);
            
            if (downloadPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                Log.Information("Extracting update archive...");
                ZipFile.ExtractToDirectory(downloadPath, extractDir);
            }
            else
            {
                // Single file executable - just copy it
                var exeName = OperatingSystem.IsWindows() ? "Seekr.exe" : "Seekr";
                File.Copy(downloadPath, Path.Combine(extractDir, exeName), true);
            }
            
            // Step 3: Save current settings to ensure they persist
            progress?.Report(new DownloadProgress { Status = "Preserving settings..." });
            SettingsService.Save();
            Log.Information("Settings saved before update");
            
            // Step 4: Create update script that will:
            // - Wait for current app to exit
            // - Copy new files (but preserve settings and logs)
            // - Restart the app
            var updateScript = CreateUpdateScript(appDir, extractDir, tempDir);
            
            progress?.Report(new DownloadProgress { Status = "Installing update..." });
            Log.Information("Launching update installer...");
            
            // Step 5: Launch the update script and exit
            LaunchUpdateScript(updateScript);
            
            return true;
        }
        catch (OperationCanceledException)
        {
            Log.Information("Update download cancelled by user");
            CleanupTempDir(tempDir);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download and install update");
            CleanupTempDir(tempDir);
            return false;
        }
    }
    
    /// <summary>
    /// Create a platform-specific update script
    /// </summary>
    private static string CreateUpdateScript(string appDir, string extractDir, string tempDir)
    {
        var currentExe = Environment.ProcessPath ?? Path.Combine(appDir, "Seekr.exe");
        var currentPid = Environment.ProcessId;
        var settingsFile = "seekr-settings.json";
        var logsDir = "logs";
        
        if (OperatingSystem.IsWindows())
        {
            var scriptPath = Path.Combine(tempDir, "update.bat");
            var script = $@"@echo off
echo Seekr Update in Progress...
echo Waiting for application to close...

:waitloop
tasklist /FI ""PID eq {currentPid}"" 2>NUL | find /I /N ""{currentPid}"">NUL
if ""%ERRORLEVEL%""==""0"" (
    timeout /t 1 /nobreak >nul
    goto waitloop
)

echo Application closed. Installing update...
timeout /t 1 /nobreak >nul

rem Backup settings and logs
if exist ""{appDir}{settingsFile}"" (
    copy /Y ""{appDir}{settingsFile}"" ""{tempDir}\{settingsFile}.bak"" >nul
)
if exist ""{appDir}{logsDir}"" (
    xcopy /E /I /Y ""{appDir}{logsDir}"" ""{tempDir}\logs_bak"" >nul
)

rem Copy new files
xcopy /E /Y ""{extractDir}\*"" ""{appDir}"" >nul

rem Restore settings and logs
if exist ""{tempDir}\{settingsFile}.bak"" (
    copy /Y ""{tempDir}\{settingsFile}.bak"" ""{appDir}{settingsFile}"" >nul
)
if exist ""{tempDir}\logs_bak"" (
    xcopy /E /I /Y ""{tempDir}\logs_bak"" ""{appDir}{logsDir}"" >nul
)

echo Update complete! Starting Seekr...
start """" ""{currentExe}""

rem Cleanup
timeout /t 2 /nobreak >nul
rd /s /q ""{tempDir}""
";
            File.WriteAllText(scriptPath, script);
            return scriptPath;
        }
        else
        {
            // Unix-like systems (Linux, macOS)
            var scriptPath = Path.Combine(tempDir, "update.sh");
            var exeName = OperatingSystem.IsMacOS() ? "Seekr" : "Seekr";
            var script = $@"#!/bin/bash
echo ""Seekr Update in Progress...""
echo ""Waiting for application to close...""

while kill -0 {currentPid} 2>/dev/null; do
    sleep 1
done

echo ""Application closed. Installing update...""
sleep 1

# Backup settings and logs
if [ -f ""{appDir}/{settingsFile}"" ]; then
    cp ""{appDir}/{settingsFile}"" ""{tempDir}/{settingsFile}.bak""
fi
if [ -d ""{appDir}/{logsDir}"" ]; then
    cp -r ""{appDir}/{logsDir}"" ""{tempDir}/logs_bak""
fi

# Copy new files
cp -rf ""{extractDir}/""* ""{appDir}/""

# Restore settings and logs
if [ -f ""{tempDir}/{settingsFile}.bak"" ]; then
    cp ""{tempDir}/{settingsFile}.bak"" ""{appDir}/{settingsFile}""
fi
if [ -d ""{tempDir}/logs_bak"" ]; then
    cp -r ""{tempDir}/logs_bak/"" ""{appDir}/{logsDir}/""
fi

# Make executable
chmod +x ""{appDir}/{exeName}""

echo ""Update complete! Starting Seekr...""
nohup ""{appDir}/{exeName}"" &>/dev/null &

# Cleanup
sleep 2
rm -rf ""{tempDir}""
";
            File.WriteAllText(scriptPath, script);
            
            // Make script executable
            Process.Start("chmod", $"+x \"{scriptPath}\"")?.WaitForExit();
            
            return scriptPath;
        }
    }
    
    /// <summary>
    /// Launch the update script and request app shutdown
    /// </summary>
    private static void LaunchUpdateScript(string scriptPath)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = true,
            CreateNoWindow = false
        };
        
        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/c \"{scriptPath}\"";
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
        }
        else
        {
            startInfo.FileName = "/bin/bash";
            startInfo.Arguments = scriptPath;
        }
        
        Process.Start(startInfo);
    }
    
    private static void CleanupTempDir(string tempDir)
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
        catch { /* Ignore cleanup errors */ }
    }
    
    /// <summary>
    /// Compare two semantic version strings
    /// </summary>
    private static bool IsNewerVersion(string latest, string current)
    {
        try
        {
            var latestParts = latest.Split('.').Select(int.Parse).ToArray();
            var currentParts = current.Split('.').Select(int.Parse).ToArray();
            
            for (int i = 0; i < Math.Min(latestParts.Length, currentParts.Length); i++)
            {
                if (latestParts[i] > currentParts[i]) return true;
                if (latestParts[i] < currentParts[i]) return false;
            }
            
            return latestParts.Length > currentParts.Length;
        }
        catch
        {
            // If parsing fails, do string comparison
            return string.Compare(latest, current, StringComparison.Ordinal) > 0;
        }
    }
    
    /// <summary>
    /// Get platform-specific filename suffix for downloads
    /// </summary>
    private static string GetPlatformSuffix()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsLinux()) return "linux";
        if (OperatingSystem.IsMacOS())
        {
            // Check for ARM vs Intel
            return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == 
                   System.Runtime.InteropServices.Architecture.Arm64 ? "macos-arm64" : "macos-x64";
        }
        return "unknown";
    }
    
    /// <summary>
    /// Get human-readable file size
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
    
    /// <summary>
    /// Open the releases page in the default browser
    /// </summary>
    public static void OpenReleasesPage()
    {
        try
        {
            var url = ReleasesPageUrl;
            
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", url);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Failed to open releases page: {Error}", ex.Message);
        }
    }
}

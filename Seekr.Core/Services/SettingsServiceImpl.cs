using System.Text.Json;
using Seekr.Models;
using Seekr.Core.Services.Abstractions;
using Serilog;
using System.Reflection;

namespace Seekr.Services;

/// <summary>
/// Non-static settings service that can be injected via DI
/// </summary>
public class SettingsServiceImpl : ISettingsService
{
    private static readonly string SettingsFolder;
    private static readonly string SettingsFile;
    private static readonly object _settingsLock = new();

    static SettingsServiceImpl()
    {
        var exePath = AppContext.BaseDirectory;
        SettingsFolder = exePath;
        SettingsFile = Path.Combine(SettingsFolder, "seekr-settings.json");

        Log.Information("Settings file location: {SettingsFile}", SettingsFile);
    }

    public AppSettings Settings
    {
        get { lock (_settingsLock) { return _settings; } }
        private set { lock (_settingsLock) { _settings = value; } }
    }
    private static AppSettings _settings = new();

    public bool LoadFailed { get; private set; }

    public void Load()
    {
        LoadFailed = false;
        lock (_settingsLock)
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        _settings = loaded;
                        Log.Information("Settings loaded successfully");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load settings");
                LoadFailed = true;
                try
                {
                    if (File.Exists(SettingsFile))
                    {
                        var backupPath = SettingsFile + ".corrupted.bak";
                        File.Copy(SettingsFile, backupPath, true);
                        Log.Warning("Corrupted settings file backed up to {BackupPath}", backupPath);
                    }
                }
                catch { /* ignore backup failure */ }
            }

            _settings = new AppSettings();
        }
    }

    public void Save()
    {
        lock (_settingsLock)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_settings, options);

                var tempFile = SettingsFile + ".tmp";
                File.WriteAllText(tempFile, json);
                File.Move(tempFile, SettingsFile, true);

                Log.Information("Settings saved to {SettingsFile}", SettingsFile);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save settings to {SettingsFile}", SettingsFile);
            }
        }
    }
}
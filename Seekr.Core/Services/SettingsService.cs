using System.Text.Json;
using Seekr.Models;
using Serilog;
using System.Reflection;

namespace Seekr.Services;

public static class SettingsService
{
    private static readonly string SettingsFolder;
    private static readonly string SettingsFile;

    static SettingsService()
    {
        // Portable mode: Save settings next to executable
        var exePath = AppContext.BaseDirectory;
        SettingsFolder = exePath;
        SettingsFile = Path.Combine(SettingsFolder, "seekr-settings.json");
        
        Log.Information("Settings file location: {SettingsFile}", SettingsFile);
    }

    public static AppSettings Settings { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    Settings = loaded;
                    Log.Information("Settings loaded successfully");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings");
            // Backup corrupted file so user doesn't lose it permanently
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

        Settings = new AppSettings(); // Fallback
    }

    public static void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Settings, options);
            
            // Atomic save: Write to temp file then move
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

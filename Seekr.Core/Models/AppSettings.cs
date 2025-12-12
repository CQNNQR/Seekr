using System.Text.Json;

namespace Seekr.Models;

public class AppSettings
{
    public ScanOptions ScanOptions { get; set; } = new();
    
    // Appearance
    public string Theme { get; set; } = "Light";
    public string SizeUnit { get; set; } = "Auto"; // Auto, Bytes, KB, MB, GB, TB
    
    // Default Views
    public string DefaultViewMode { get; set; } = "Tree"; // Tree, Pie, Bar, Treemap, TopFiles
    public string DefaultGraph { get; set; } = "Pie"; // Pie, Bar, Treemap
    
    // Chart Settings
    public int MaxPieSlices { get; set; } = 10;
    public int MaxBarItems { get; set; } = 15;
    public int MaxTopFiles { get; set; } = 100;
    public double MinSlicePercentage { get; set; } = 2.0; // Minimum % to show as separate slice
    
    // Behavior
    public bool RememberLastPath { get; set; } = true;
    public string LastScanPath { get; set; } = string.Empty;
    public bool ConfirmBeforeDelete { get; set; } = true;
    public bool ShowHiddenFiles { get; set; } = true;
    public bool ShowSystemFiles { get; set; } = true;
    
    // Telemetry & Updates
    public bool SendAnonymousUsageData { get; set; } = true;  // Opt-in by default, can disable
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public bool HasShownTelemetryConsent { get; set; } = false;  // First-run flag
}

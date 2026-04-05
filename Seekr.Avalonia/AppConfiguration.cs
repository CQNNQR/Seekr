using System;

namespace Seekr.Avalonia;

public static class AppConfiguration
{
    // ==================== Throttle Durations ====================
    /// <summary>
    /// Throttle duration for search input (milliseconds).
    /// </summary>
    public const int SearchThrottleMs = 500;

    /// <summary>
    /// Throttle duration for chart selection updates (milliseconds).
    /// </summary>
    public const int ChartSelectionThrottleMs = 200;

    // ==================== Timeout Durations ====================
    /// <summary>
    /// Context menu auto-close timeout (seconds).
    /// </summary>
    public const int ContextMenuTimeoutSec = 10;

    /// <summary>
    /// Process wait timeout for file operations (milliseconds).
    /// </summary>
    public const int ProcessWaitTimeoutMs = 5000;

    /// <summary>
    /// Telemetry notice display duration (milliseconds).
    /// </summary>
    public const int TelemetryNoticeDurationMs = 3000;

    /// <summary>
    /// Telemetry fade animation duration (milliseconds).
    /// </summary>
    public const int TelemetryFadeDurationMs = 500;

    // ==================== Buffer Sizes ====================
    /// <summary>
    /// Buffer size for fast file hashing (bytes).
    /// Used for quick duplicate detection using first/last bytes + size.
    /// </summary>
    public const int FastHashBufferSize = 8192;

    /// <summary>
    /// Buffer size for file sampling during content verification (bytes).
    /// Samples from start and end of file.
    /// </summary>
    public const int FileSamplingBufferSize = 4096;

    // ==================== Duplicate Detection Limits ====================
    /// <summary>
    /// Maximum number of duplicate groups to display in results.
    /// </summary>
    public const int MaxDuplicateGroupsDisplay = 500;

    /// <summary>
    /// Minimum file size to consider for duplicate detection (bytes).
    /// Files smaller than this are skipped in the initial fingerprint pass.
    /// </summary>
    public const int MinFileSizeForDuplicateCheck = 1024; // 1 KB

    // ==================== UI Constants ====================
    /// <summary>
    /// Number of steps in the fade animation.
    /// </summary>
    public const int TelemetryFadeSteps = 10;

    /// <summary>
    /// Delay between each fade step (milliseconds).
    /// </summary>
    public const int TelemetryFadeStepDelayMs = 50;

    /// <summary>
    /// Status bar progress bar width.
    /// </summary>
    public const int StatusProgressBarWidth = 150;

    /// <summary>
    /// Progress bar height in update banner.
    /// </summary>
    public const int UpdateProgressBarHeight = 8;

    /// <summary>
    /// Search text box width.
    /// </summary>
    public const int SearchTextBoxWidth = 200;

    /// <summary>
    /// Combo box width for duplicate keep strategy.
    /// </summary>
    public const int DuplicateStrategyComboBoxWidth = 150;

    // ==================== TimeSpan Helpers ====================
    public static readonly TimeSpan SearchThrottle = TimeSpan.FromMilliseconds(SearchThrottleMs);
    public static readonly TimeSpan ChartSelectionThrottle = TimeSpan.FromMilliseconds(ChartSelectionThrottleMs);
    public static readonly TimeSpan ContextMenuTimeout = TimeSpan.FromSeconds(ContextMenuTimeoutSec);
    public static readonly TimeSpan ProcessWaitTimeout = TimeSpan.FromMilliseconds(ProcessWaitTimeoutMs);
    public static readonly TimeSpan TelemetryNoticeDuration = TimeSpan.FromMilliseconds(TelemetryNoticeDurationMs);
}

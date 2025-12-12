using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using System.Collections.Concurrent;

namespace Seekr.Services;

/// <summary>
/// Central Serilog initialization helper.
/// Configure sinks and call LogService.Init() at application startup.
/// </summary>
public static class LogService
{
    // Thread-safe in-memory ring buffer for recent log lines. UI can read this to show diagnostics in-app.
    private static readonly ConcurrentQueue<string> _recentLines = new();
    private const int MaxRecentLines = 1000;
    private static readonly MessageTemplateTextFormatter _lineFormatter = new("{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

    // Expose read access for UI
    public static IReadOnlyList<string> GetRecentLines(int take = 200)
    {
        var arr = _recentLines.ToArray();
        var last = arr.Skip(Math.Max(0, arr.Length - take)).ToArray();
        return Array.AsReadOnly(last);
    }

    public static void ClearRecentLines()
    {
        while (_recentLines.TryDequeue(out _)) { }
    }

    public static void Init()
    {
        try
        {
            var seqUrl = Environment.GetEnvironmentVariable("SEEK_SEQ_URL") ?? "http://localhost:5341";
            var seqApiKey = Environment.GetEnvironmentVariable("SEEK_SEQ_API_KEY");

            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);

            var loggerCfg = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(logDir, "Seekr-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                // Always write into our in-memory diagnostics buffer so the UI can show recent logs
                .WriteTo.Sink(new InMemorySink());

            // Add Seq sink only if explicitly enabled (SEEK_SEQ_ENABLED=1 or true)
            var seqEnabledRaw = Environment.GetEnvironmentVariable("SEEK_SEQ_ENABLED");
            var seqEnabled = !string.IsNullOrEmpty(seqEnabledRaw) && (seqEnabledRaw == "1" || seqEnabledRaw.Equals("true", StringComparison.OrdinalIgnoreCase));
            if (seqEnabled && !string.IsNullOrWhiteSpace(seqUrl))
            {
                loggerCfg = loggerCfg.WriteTo.Seq(seqUrl, apiKey: seqApiKey);
            }

            Log.Logger = loggerCfg.CreateLogger();
            Log.Verbose("Logger initialized (Seq: {SeqUrl} enabled={SeqEnabled})", seqUrl, seqEnabled);
        }
        catch (Exception ex)
        {
            // Try a minimal fallback so we don't lose unhandled exceptions
            try
            {
                Log.Logger = new LoggerConfiguration().WriteTo.File("logs/Seekr-fallback.log").CreateLogger();
                Log.Error(ex, "Failed to initialize primary logger");
            }
            catch { /* ignore */ }
        }
    }

    public static void Shutdown()
    {
        try
        {
            Log.Verbose("Shutting down logger");
            Log.CloseAndFlush();
        }
        catch { }
    }

    private class InMemorySink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            try
            {
                using var sw = new System.IO.StringWriter();
                _lineFormatter.Format(logEvent, sw);
                var line = sw.ToString();

                _recentLines.Enqueue(line);
                // Trim if necessary
                while (_recentLines.Count > MaxRecentLines)
                {
                    _recentLines.TryDequeue(out _);
                }
            }
            catch
            {
                // best-effort
            }
        }
    }
}

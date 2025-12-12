using Seekr.Forms;
using Serilog;
using Seekr.Services;

namespace Seekr;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Wrap entire startup in try/catch and attach domain/UI exception handlers
        try
        {
            // Initialize structured logging
            LogService.Init();
            
            // Load settings
            Services.SettingsService.Load();

            // Attach handlers for UI thread exceptions
            Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) => Log.Error(e.Exception, "Unhandled UI thread exception");

            // Attach handler for non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Log.Fatal(ex, "Unhandled non-UI exception");
            };

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal exception in Main");
            // Re-throw so the outer host receives the failure status
            throw;
        }
        finally
        {
            // Ensure logs are flushed on exit
            LogService.Shutdown();
        }
    }

    // Keep the existing local fallback file logging for cases where Serilog initialization fails.
    private static void LogException(Exception? ex, string kind)
    {
        try
        {
            var message = ex != null ? ex.ToString() : "(no exception)";
            // Attempt to write via Serilog if configured
            try
            {
                if (ex != null)
                {
                    Log.Error(ex, "{Kind}: {Message}", kind, ex.Message);
                }
                else
                {
                    Log.Warning("{Kind}: exception object was null", kind);
                }
            }
            catch { /* ignore Serilog failures */ }

            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"seekr-{kind}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(logFile, $"Kind: {kind}\nTimestamp: {DateTime.Now:O}\n{message}");
        }
        catch { }
    }
}

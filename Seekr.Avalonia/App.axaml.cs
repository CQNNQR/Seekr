using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Seekr.Core.Services.Abstractions;
using Serilog;

namespace Seekr.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Load settings and apply saved theme
        try
        {
            var settingsService = Program.Services?.GetService<ISettingsService>();
            settingsService?.Load();
            var theme = settingsService?.Settings?.Theme;
            if (theme == "Dark")
            {
                RequestedThemeVariant = ThemeVariant.Dark;
            }
            else if (theme == "Light")
            {
                RequestedThemeVariant = ThemeVariant.Light;
            }

            Log.Information("Applied theme from settings: {Theme}", theme ?? "System Default");

            // Initialize telemetry based on settings
            var telemetryService = Program.Services?.GetService<ITelemetryService>();
            if (telemetryService != null)
            {
                telemetryService.IsEnabled = settingsService?.Settings?.SendAnonymousUsageData ?? true;

                if (telemetryService.IsEnabled)
                {
                    _ = telemetryService.TrackAppLaunchAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings on startup");
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Program.Services?.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
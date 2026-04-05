using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Seekr.Core.Services.Abstractions;
using Seekr.Services;
using Serilog;
using System;

namespace Seekr.Avalonia;

class Program
{
    public static IServiceProvider? Services { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/seekr.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            // Set up dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register services
        services.AddSingleton<IDiskScanner, DiskScanner>();
        services.AddSingleton<IAnalysisService, AnalysisService>();
        services.AddSingleton<ISettingsService, SettingsServiceImpl>();
        services.AddSingleton<ITelemetryService, TelemetryServiceImpl>();
        services.AddSingleton<IUpdateService, UpdateServiceImpl>();

        // Register ViewModels
        services.AddTransient<ViewModels.MainWindowViewModel>();
        services.AddTransient<ViewModels.SettingsWindowViewModel>();
        services.AddTransient<ViewModels.DetailsWindowViewModel>();

        // Register Windows
        services.AddTransient<MainWindow>();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
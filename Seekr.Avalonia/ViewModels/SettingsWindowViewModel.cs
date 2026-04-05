using System;
using System.Reactive;
using ReactiveUI;
using Seekr.Core.Services.Abstractions;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;

namespace Seekr.Avalonia.ViewModels;

public class SettingsWindowViewModel : ViewModelBase
{
    private int _selectedThemeIndex;
    private int _selectedUnitIndex;
    private int _selectedDefaultGraphIndex;
    private decimal _maxPieSlices;
    private decimal _maxBarItems;
    private decimal _maxTopFiles;
    private decimal _minSlicePercentage;
    private bool _rememberLastPath;
    private bool _confirmBeforeDelete;
    private bool _showHiddenFiles;
    private bool _showSystemFiles;
    private bool _checkForUpdates;
    private bool _sendUsageData;

    private readonly ISettingsService _settingsService;

    public SettingsWindowViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsService.Load();
        var settings = _settingsService.Settings;

        SelectedThemeIndex = settings.Theme == "Dark" ? 1 : 0;

        SelectedUnitIndex = settings.SizeUnit switch
        {
            "Bytes" => 1,
            "KB" => 2,
            "MB" => 3,
            "GB" => 4,
            _ => 0
        };

        SelectedDefaultGraphIndex = settings.DefaultGraph switch
        {
            "Pie" => 0,
            "Bar" => 1,
            "Treemap" => 2,
            _ => 0
        };

        MaxPieSlices = settings.MaxPieSlices;
        MaxBarItems = settings.MaxBarItems;
        MaxTopFiles = settings.MaxTopFiles;
        MinSlicePercentage = (decimal)settings.MinSlicePercentage;

        RememberLastPath = settings.RememberLastPath;
        ConfirmBeforeDelete = settings.ConfirmBeforeDelete;
        ShowHiddenFiles = settings.ShowHiddenFiles;
        ShowSystemFiles = settings.ShowSystemFiles;
        CheckForUpdates = settings.CheckForUpdatesOnStartup;
        SendUsageData = settings.SendAnonymousUsageData;

        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(Cancel);
    }

    public int SelectedThemeIndex
    {
        get => _selectedThemeIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedThemeIndex, value);
    }

    public int SelectedUnitIndex
    {
        get => _selectedUnitIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedUnitIndex, value);
    }

    public int SelectedDefaultGraphIndex
    {
        get => _selectedDefaultGraphIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedDefaultGraphIndex, value);
    }

    public decimal MaxPieSlices
    {
        get => _maxPieSlices;
        set => this.RaiseAndSetIfChanged(ref _maxPieSlices, value);
    }

    public decimal MaxBarItems
    {
        get => _maxBarItems;
        set => this.RaiseAndSetIfChanged(ref _maxBarItems, value);
    }

    public decimal MaxTopFiles
    {
        get => _maxTopFiles;
        set => this.RaiseAndSetIfChanged(ref _maxTopFiles, value);
    }

    public decimal MinSlicePercentage
    {
        get => _minSlicePercentage;
        set => this.RaiseAndSetIfChanged(ref _minSlicePercentage, value);
    }

    public bool RememberLastPath
    {
        get => _rememberLastPath;
        set => this.RaiseAndSetIfChanged(ref _rememberLastPath, value);
    }

    public bool ConfirmBeforeDelete
    {
        get => _confirmBeforeDelete;
        set => this.RaiseAndSetIfChanged(ref _confirmBeforeDelete, value);
    }

    public bool ShowHiddenFiles
    {
        get => _showHiddenFiles;
        set => this.RaiseAndSetIfChanged(ref _showHiddenFiles, value);
    }

    public bool ShowSystemFiles
    {
        get => _showSystemFiles;
        set => this.RaiseAndSetIfChanged(ref _showSystemFiles, value);
    }

    public bool CheckForUpdates
    {
        get => _checkForUpdates;
        set => this.RaiseAndSetIfChanged(ref _checkForUpdates, value);
    }

    public bool SendUsageData
    {
        get => _sendUsageData;
        set => this.RaiseAndSetIfChanged(ref _sendUsageData, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public event Action? RequestClose;
    public bool SettingsSaved { get; private set; }

    private void Save()
    {
        var settings = _settingsService.Settings;

        settings.Theme = SelectedThemeIndex == 1 ? "Dark" : "Light";
        settings.SizeUnit = SelectedUnitIndex switch
        {
            1 => "Bytes",
            2 => "KB",
            3 => "MB",
            4 => "GB",
            _ => "Auto"
        };

        settings.DefaultGraph = SelectedDefaultGraphIndex switch
        {
            1 => "Bar",
            2 => "Treemap",
            _ => "Pie"
        };

        settings.MaxPieSlices = (int)MaxPieSlices;
        settings.MaxBarItems = (int)MaxBarItems;
        settings.MaxTopFiles = (int)MaxTopFiles;
        settings.MinSlicePercentage = (double)MinSlicePercentage;

        settings.RememberLastPath = RememberLastPath;
        settings.ConfirmBeforeDelete = ConfirmBeforeDelete;
        settings.ShowHiddenFiles = ShowHiddenFiles;
        settings.ShowSystemFiles = ShowSystemFiles;

        settings.CheckForUpdatesOnStartup = CheckForUpdates;
        settings.SendAnonymousUsageData = SendUsageData;

        settings.ScanOptions.ScanHiddenFiles = ShowHiddenFiles;
        settings.ScanOptions.ScanSystemFiles = ShowSystemFiles;

        _settingsService.Save();

        if (global::Avalonia.Application.Current != null)
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = settings.Theme == "Dark"
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }

        SettingsSaved = true;
        RequestClose?.Invoke();
    }

    private void Cancel()
    {
        SettingsSaved = false;
        RequestClose?.Invoke();
    }
}
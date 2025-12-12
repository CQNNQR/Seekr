using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using ReactiveUI;
using Seekr.Models;
using Seekr.Services;
using SkiaSharp;
using Serilog;
using LiveChartsCore.Defaults;

namespace Seekr.Avalonia.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _currentPath = string.Empty;
    private string _statusMessage = "Ready";
    private bool _isScanning;
    private ObservableCollection<FileSystemNode> _nodes = new();
    private DiskScanner _scanner;
    private readonly AnalysisService _analysisService;

    // Chart Properties
    private ISeries[] _pieSeries = Array.Empty<ISeries>();
    private ISeries[] _barSeries = Array.Empty<ISeries>();
    private Axis[] _barXAxes = Array.Empty<Axis>();
    private Axis[] _barYAxes = Array.Empty<Axis>();
    private ObservableCollection<FileSystemNode> _topFiles = new();
    private FileSystemNode? _selectedNode;
    private FileSystemNode? _otherVirtualNode;
    private List<FileSystemNode> _currentBarItems = new();

    private string _searchText = string.Empty;
    private ObservableCollection<FileSystemNode> _searchResults = new();
    private bool _isSearchActive;
    private bool _hasResults;
    private int _selectedTabIndex;
    
    // Multi-path scanning
    private ObservableCollection<string> _scanPaths = new();
    private string _scanPathsDisplay = "Select folder(s) to scan...";
    private bool _hasMultiplePaths;
    
    // File Type Analysis
    private ObservableCollection<FileTypeInfo> _fileTypeAnalysis = new();
    
    // Duplicate Detection
    private ObservableCollection<DuplicateGroup> _duplicateGroups = new();
    private string _duplicatesStatus = "Click 'Find Duplicates' to scan for duplicate files";
    private string _searchResultsStatus = "";
    private CancellationTokenSource? _duplicateScanCts;
    private bool _isDuplicateScanning;
    private bool _hasHddDrives;
    private bool _needsVerification;
    
    // Update System
    private bool _isUpdateAvailable;
    private string _updateVersion = string.Empty;
    private string _updateStatus = string.Empty;
    private bool _isDownloadingUpdate;
    private double _updateDownloadProgress;
    private UpdateService.UpdateInfo? _pendingUpdate;

    public MainWindowViewModel()
    {
        _scanner = new DiskScanner(SettingsService.Settings?.ScanOptions);
        _analysisService = new AnalysisService();
        
        // Load last path if "Remember Last Path" is enabled
        if (SettingsService.Settings?.RememberLastPath == true && 
            !string.IsNullOrEmpty(SettingsService.Settings.LastScanPath))
        {
            var paths = SettingsService.Settings.LastScanPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in paths)
            {
                if (!string.IsNullOrWhiteSpace(p))
                    _scanPaths.Add(p.Trim());
            }
            UpdateScanPathsDisplay();
        }
        
        ScanCommand = ReactiveCommand.CreateFromTask(ScanAsync, 
            this.WhenAnyValue(x => x.ScanPaths.Count, x => x.IsScanning, 
                (count, scanning) => count > 0 && !scanning));
        
        CancelCommand = ReactiveCommand.Create(() => _scanner.Cancel());

        SearchCommand = ReactiveCommand.CreateFromTask(PerformSearchAsync);
        ClearSearchCommand = ReactiveCommand.Create(ClearSearch);
        GoToNodeCommand = ReactiveCommand.Create<FileSystemNode>(GoToNode);
        
        ExportCommand = ReactiveCommand.CreateFromTask(ExportAsync,
            this.WhenAnyValue(x => x.HasResults));

        // Auto-search when text changes (optional, or keep it manual)
        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Select(_ => Unit.Default)
            .ObserveOn(RxApp.MainThreadScheduler)
            .InvokeCommand(SearchCommand);

        OpenCommand = ReactiveCommand.Create<FileSystemNode>(OpenItem);
        RevealCommand = ReactiveCommand.Create<FileSystemNode>(RevealItem);
        CopyPathCommand = ReactiveCommand.Create<FileSystemNode>(CopyPath);
        DeleteCommand = ReactiveCommand.CreateFromTask<FileSystemNode>(DeleteItemAsync);
        ExportNodeCommand = ReactiveCommand.CreateFromTask<FileSystemNode>(ExportNodeAsync);
        OpenSettingsCommand = ReactiveCommand.Create(OpenSettings);
        ChartPointPointerDownCommand = ReactiveCommand.Create<object>(OnChartPointPointerDown);
        NavigateUpCommand = ReactiveCommand.Create(NavigateUp, 
            this.WhenAnyValue(x => x.SelectedNode, x => x.SelectedNode.Parent, 
                (node, parent) => node != null && parent != null));
        FindDuplicatesCommand = ReactiveCommand.CreateFromTask(FindDuplicatesAsync,
            this.WhenAnyValue(x => x.HasResults, x => x.IsDuplicateScanning,
                (hasResults, isScanning) => hasResults && !isScanning));
        CancelDuplicateScanCommand = ReactiveCommand.Create(CancelDuplicateScan,
            this.WhenAnyValue(x => x.IsDuplicateScanning));
        VerifyDuplicatesCommand = ReactiveCommand.CreateFromTask(VerifyDuplicatesAsync,
            this.WhenAnyValue(x => x.NeedsVerification, x => x.IsDuplicateScanning,
                (needs, isScanning) => needs && !isScanning));

        // Update charts when selection changes
        this.WhenAnyValue(x => x.SelectedNode)
            .Where(node => node != null)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async node => 
            {
                if (node != null) await GenerateChartsAsync(node);
            });

        // Apply default graph tab from settings
        SelectedTabIndex = SettingsService.Settings?.DefaultGraph switch
        {
            "Pie" => 0,
            "Bar" => 1,
            "Treemap" => 2,
            _ => 0
        };
        
        // Update commands
        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdatesAsync);
        InstallUpdateCommand = ReactiveCommand.CreateFromTask(InstallUpdateAsync,
            this.WhenAnyValue(x => x.IsUpdateAvailable, x => x.IsDownloadingUpdate,
                (available, downloading) => available && !downloading));
        DismissUpdateCommand = ReactiveCommand.Create(() => IsUpdateAvailable = false);
        
        // Check for updates on startup if enabled
        if (SettingsService.Settings?.CheckForUpdatesOnStartup == true)
        {
            _ = CheckForUpdatesAsync();
        }
    }

    public ICommand ScanCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand RevealCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ExportNodeCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ChartPointPointerDownCommand { get; }
    public ICommand NavigateUpCommand { get; }
    public ICommand FindDuplicatesCommand { get; }
    public ICommand CancelDuplicateScanCommand { get; }
    public ICommand VerifyDuplicatesCommand { get; }
    public ICommand GoToNodeCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand InstallUpdateCommand { get; }
    public ICommand DismissUpdateCommand { get; }

    // Helper methods for context menu access
    public FileSystemNode? GetOtherVirtualNode() => _otherVirtualNode;
    public FileSystemNode? GetBarItemAtIndex(int index) => 
        index >= 0 && index < _currentBarItems.Count ? _currentBarItems[index] : null;
    
    // Flag to prevent navigation on right-click
    public bool SkipNextChartClick { get; set; }

    private void NavigateUp()
    {
        if (SelectedNode?.Parent != null)
        {
            SelectedNode = SelectedNode.Parent;
        }
    }

    private SKColor GetColorForIndex(int index, int total)
    {
        if (total <= 1) return SKColors.Red;
        
        // Map index 0 (Largest) -> Pastel Red (Hue 0)
        // Map index total-1 (Smallest) -> Pastel Blue (Hue 240)
        
        float ratio = (float)index / (total - 1);
        float hue = 240f * ratio; 
        
        // Pastel: Saturation ~70%, Lightness ~80%
        return SKColor.FromHsl(hue, 70, 60);
    }

    private void OnChartPointPointerDown(object arg)
    {
        // Skip if this was a right-click (context menu) or if handled by our custom handlers
        if (SkipNextChartClick)
        {
            SkipNextChartClick = false;
            return;
        }
        
        // Note: Pie and Bar chart clicks are now handled in MainWindow.axaml.cs
        // using GetPointsAt for more reliable click detection.
        // This handler is kept for any remaining chart types that might use the command.
    }

    private void OpenSettings()
    {
        var settingsVm = new SettingsWindowViewModel();
        var settingsWindow = new SettingsWindow
        {
            DataContext = settingsVm
        };

        settingsVm.RequestClose += async () => 
        {
            settingsWindow.Close();
            
            // If settings were saved and we have a selected node, refresh charts with new theme/settings
            if (settingsVm.SettingsSaved && SelectedNode != null)
            {
                await GenerateChartsAsync(SelectedNode);
            }
        };

        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            settingsWindow.ShowDialog(desktop.MainWindow);
        }
    }
    
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            UpdateStatus = "Checking for updates...";
            Log.Information("Manually checking for updates...");
            
            var updateInfo = await UpdateService.CheckForUpdatesAsync();
            _pendingUpdate = updateInfo;
            
            if (updateInfo.IsUpdateAvailable)
            {
                UpdateVersion = updateInfo.LatestVersion;
                var fileSize = updateInfo.FileSizeBytes > 0 
                    ? $" ({UpdateService.FormatFileSize(updateInfo.FileSizeBytes)})" 
                    : "";
                UpdateStatus = $"Version {updateInfo.LatestVersion} available{fileSize}";
                IsUpdateAvailable = true;
                Log.Information("Update available: v{Version}", updateInfo.LatestVersion);
            }
            else
            {
                UpdateStatus = "You're running the latest version!";
                IsUpdateAvailable = false;
                
                // Auto-hide after 3 seconds
                await Task.Delay(3000);
                if (UpdateStatus == "You're running the latest version!")
                {
                    UpdateStatus = string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Update check failed: {ex.Message}";
            Log.Error(ex, "Update check failed");
        }
    }
    
    private async Task InstallUpdateAsync()
    {
        if (_pendingUpdate == null || !_pendingUpdate.IsUpdateAvailable)
        {
            UpdateStatus = "No update available";
            return;
        }
        
        if (string.IsNullOrEmpty(_pendingUpdate.DownloadUrl))
        {
            // No direct download - open the releases page instead
            UpdateService.OpenReleasesPage();
            UpdateStatus = "Opening download page...";
            return;
        }
        
        try
        {
            IsDownloadingUpdate = true;
            UpdateDownloadProgress = 0;
            
            var progress = new Progress<UpdateService.DownloadProgress>(p =>
            {
                UpdateDownloadProgress = p.PercentComplete;
                UpdateStatus = p.Status;
            });
            
            var success = await UpdateService.DownloadAndInstallUpdateAsync(_pendingUpdate, progress);
            
            if (success)
            {
                UpdateStatus = "Update downloaded! Restarting...";
                
                // Give a moment for the user to see the message, then exit
                await Task.Delay(1500);
                
                // Request app shutdown - the update script will handle the rest
                if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
            else
            {
                UpdateStatus = "Update failed. Try downloading manually.";
                IsDownloadingUpdate = false;
            }
        }
        catch (OperationCanceledException)
        {
            UpdateStatus = "Update cancelled";
            IsDownloadingUpdate = false;
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Update failed: {ex.Message}";
            IsDownloadingUpdate = false;
            Log.Error(ex, "Update installation failed");
        }
    }

    private void OpenItem(FileSystemNode? node)
    {
        if (node == null) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = node.FullPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening item: {ex.Message}";
        }
    }

    private void RevealItem(FileSystemNode? node)
    {
        if (node == null) return;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{node.FullPath}\"");
            }
            else if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start("open", $"-R \"{node.FullPath}\"");
            }
            else if (OperatingSystem.IsLinux())
            {
                // Try dbus-send or xdg-open (xdg-open usually opens the folder but doesn't select)
                System.Diagnostics.Process.Start("xdg-open", System.IO.Path.GetDirectoryName(node.FullPath) ?? "/");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error revealing item: {ex.Message}";
        }
    }

    private async void CopyPath(FileSystemNode? node)
    {
        if (node == null) return;
        try
        {
            var topLevel = TopLevel.GetTopLevel(
                (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
            
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(node.FullPath);
                StatusMessage = "Path copied to clipboard.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error copying path: {ex.Message}";
        }
    }

    private async Task DeleteItemAsync(FileSystemNode? node)
    {
        if (node == null) return;
        
        // Don't allow deleting the virtual "Other" node
        if (node.Name == "Other" && node.FullPath.Contains("[Other smaller items]"))
        {
            StatusMessage = "Cannot delete the virtual 'Other' grouping.";
            return;
        }
        
        try
        {
            var confirmBeforeDelete = SettingsService.Settings?.ConfirmBeforeDelete ?? true;
            
            if (confirmBeforeDelete)
            {
                // Show confirmation dialog
                var mainWindow = (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                if (mainWindow == null) return;
                
                var itemType = node.IsDirectory ? "folder" : "file";
                var sizeStr = FormatSize(node.TotalSize);
                var message = $"Are you sure you want to delete this {itemType}?\n\n{node.Name}\n{sizeStr}\n\nThis action cannot be undone.";
                
                var dialog = new Window
                {
                    Title = "Confirm Delete",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false
                };
                
                var result = false;
                
                var yesButton = new Button { Content = "Delete", Background = global::Avalonia.Media.Brushes.Red, Foreground = global::Avalonia.Media.Brushes.White, Margin = new global::Avalonia.Thickness(5) };
                var noButton = new Button { Content = "Cancel", Margin = new global::Avalonia.Thickness(5) };
                
                yesButton.Click += (s, e) => { result = true; dialog.Close(); };
                noButton.Click += (s, e) => { result = false; dialog.Close(); };
                
                var buttonPanel = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center, Margin = new global::Avalonia.Thickness(0, 10, 0, 0) };
                buttonPanel.Children.Add(yesButton);
                buttonPanel.Children.Add(noButton);
                
                var messageText = new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, Margin = new global::Avalonia.Thickness(20) };
                
                var mainPanel = new StackPanel { Margin = new global::Avalonia.Thickness(10) };
                mainPanel.Children.Add(messageText);
                mainPanel.Children.Add(buttonPanel);
                
                dialog.Content = mainPanel;
                
                await dialog.ShowDialog(mainWindow);
                
                if (!result)
                {
                    StatusMessage = "Delete cancelled.";
                    return;
                }
            }
            
            // Perform the deletion
            StatusMessage = $"Deleting {node.Name}...";
            
            await Task.Run(() =>
            {
                if (node.IsDirectory)
                {
                    System.IO.Directory.Delete(node.FullPath, true);
                }
                else
                {
                    System.IO.File.Delete(node.FullPath);
                }
            });
            
            // Remove from parent's children and update UI
            if (node.Parent != null)
            {
                node.Parent.Children.Remove(node);
                
                // Recalculate parent's size
                RecalculateParentSizes(node.Parent);
                
                // Refresh charts
                if (SelectedNode != null)
                {
                    await GenerateChartsAsync(SelectedNode);
                }
            }
            
            StatusMessage = $"Deleted: {node.Name}";
            Log.Information("Deleted {ItemType}: {Path}", node.IsDirectory ? "folder" : "file", node.FullPath);
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = $"Access denied: Cannot delete {node.Name}";
        }
        catch (System.IO.IOException ex)
        {
            StatusMessage = $"Cannot delete: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting: {ex.Message}";
            Log.Error(ex, "Error deleting {Path}", node.FullPath);
        }
    }
    
    private void RecalculateParentSizes(FileSystemNode node)
    {
        // Recalculate size from children
        if (node.IsDirectory)
        {
            node.TotalSize = node.Children.Sum(c => c.TotalSize);
        }
        
        // Propagate up the tree
        if (node.Parent != null)
        {
            RecalculateParentSizes(node.Parent);
        }
    }

    public string CurrentPath
    {
        get => _currentPath;
        set => this.RaiseAndSetIfChanged(ref _currentPath, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public ObservableCollection<FileSystemNode> SearchResults
    {
        get => _searchResults;
        set => this.RaiseAndSetIfChanged(ref _searchResults, value);
    }

    public bool IsSearchActive
    {
        get => _isSearchActive;
        set => this.RaiseAndSetIfChanged(ref _isSearchActive, value);
    }

    public bool HasResults
    {
        get => _hasResults;
        set => this.RaiseAndSetIfChanged(ref _hasResults, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => this.RaiseAndSetIfChanged(ref _isScanning, value);
    }

    public ObservableCollection<FileSystemNode> Nodes
    {
        get => _nodes;
        set => this.RaiseAndSetIfChanged(ref _nodes, value);
    }

    public ISeries[] PieSeries
    {
        get => _pieSeries;
        set => this.RaiseAndSetIfChanged(ref _pieSeries, value);
    }

    public ISeries[] BarSeries
    {
        get => _barSeries;
        set => this.RaiseAndSetIfChanged(ref _barSeries, value);
    }

    public Axis[] BarXAxes
    {
        get => _barXAxes;
        set => this.RaiseAndSetIfChanged(ref _barXAxes, value);
    }

    public Axis[] BarYAxes
    {
        get => _barYAxes;
        set => this.RaiseAndSetIfChanged(ref _barYAxes, value);
    }

    public ObservableCollection<FileSystemNode> TopFiles
    {
        get => _topFiles;
        set => this.RaiseAndSetIfChanged(ref _topFiles, value);
    }

    public FileSystemNode? SelectedNode
    {
        get => _selectedNode;
        set => this.RaiseAndSetIfChanged(ref _selectedNode, value);
    }
    
    public ObservableCollection<FileTypeInfo> FileTypeAnalysis
    {
        get => _fileTypeAnalysis;
        set => this.RaiseAndSetIfChanged(ref _fileTypeAnalysis, value);
    }
    
    public ObservableCollection<DuplicateGroup> DuplicateGroups
    {
        get => _duplicateGroups;
        set => this.RaiseAndSetIfChanged(ref _duplicateGroups, value);
    }
    
    public string DuplicatesStatus
    {
        get => _duplicatesStatus;
        set => this.RaiseAndSetIfChanged(ref _duplicatesStatus, value);
    }
    
    public bool IsDuplicateScanning
    {
        get => _isDuplicateScanning;
        set => this.RaiseAndSetIfChanged(ref _isDuplicateScanning, value);
    }
    
    public bool HasHddDrives
    {
        get => _hasHddDrives;
        set => this.RaiseAndSetIfChanged(ref _hasHddDrives, value);
    }
    
    public bool NeedsVerification
    {
        get => _needsVerification;
        set => this.RaiseAndSetIfChanged(ref _needsVerification, value);
    }
    
    public string SearchResultsStatus
    {
        get => _searchResultsStatus;
        set => this.RaiseAndSetIfChanged(ref _searchResultsStatus, value);
    }
    
    // Multi-path scanning properties
    public ObservableCollection<string> ScanPaths
    {
        get => _scanPaths;
        set => this.RaiseAndSetIfChanged(ref _scanPaths, value);
    }
    
    public string ScanPathsDisplay
    {
        get => _scanPathsDisplay;
        set => this.RaiseAndSetIfChanged(ref _scanPathsDisplay, value);
    }
    
    public bool HasMultiplePaths
    {
        get => _hasMultiplePaths;
        set => this.RaiseAndSetIfChanged(ref _hasMultiplePaths, value);
    }
    
    // Update System Properties
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => this.RaiseAndSetIfChanged(ref _isUpdateAvailable, value);
    }
    
    public string UpdateVersion
    {
        get => _updateVersion;
        set => this.RaiseAndSetIfChanged(ref _updateVersion, value);
    }
    
    public string UpdateStatus
    {
        get => _updateStatus;
        set => this.RaiseAndSetIfChanged(ref _updateStatus, value);
    }
    
    public bool IsDownloadingUpdate
    {
        get => _isDownloadingUpdate;
        set => this.RaiseAndSetIfChanged(ref _isDownloadingUpdate, value);
    }
    
    public double UpdateDownloadProgress
    {
        get => _updateDownloadProgress;
        set => this.RaiseAndSetIfChanged(ref _updateDownloadProgress, value);
    }
    
    public void SetScanPath(string path)
    {
        // Replace all paths with a single one
        _scanPaths.Clear();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _scanPaths.Add(path);
        }
        UpdateScanPathsDisplay();
    }
    
    public void AddScanPath(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && 
            !_scanPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            _scanPaths.Add(path);
            UpdateScanPathsDisplay();
        }
    }
    
    public void RemoveScanPath(string path)
    {
        var existing = _scanPaths.FirstOrDefault(p => 
            string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _scanPaths.Remove(existing);
            UpdateScanPathsDisplay();
        }
    }
    
    private void UpdateScanPathsDisplay()
    {
        HasMultiplePaths = _scanPaths.Count > 1;
        
        if (_scanPaths.Count == 0)
        {
            ScanPathsDisplay = "Select folder(s) to scan...";
            CurrentPath = string.Empty;
        }
        else if (_scanPaths.Count == 1)
        {
            ScanPathsDisplay = _scanPaths[0];
            CurrentPath = _scanPaths[0];
        }
        else
        {
            ScanPathsDisplay = $"{_scanPaths.Count} folders selected";
            CurrentPath = string.Join(";", _scanPaths);
        }
        
        // Notify that ScanCommand can execute may have changed
        this.RaisePropertyChanged(nameof(ScanPaths));
    }
    
    private void ClearSearch()
    {
        SearchText = string.Empty;
        SearchResults.Clear();
        IsSearchActive = false;
        SearchResultsStatus = "";
    }
    
    public void GoToNode(FileSystemNode? node)
    {
        if (node == null) return;
        
        // Clear search to show main view
        ClearSearch();
        
        // If it's a directory, navigate to it
        if (node.IsDirectory)
        {
            SelectedNode = node;
        }
        else
        {
            // For files, navigate to parent directory
            if (node.Parent != null)
            {
                SelectedNode = node.Parent;
            }
        }
    }

    private async Task ExportAsync()
    {
        if (Nodes.Count == 0 || Nodes[0] == null) return;

        try
        {
            // Resolve TopLevel from the current application lifetime
            var topLevel = TopLevel.GetTopLevel(
                (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
            
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Scan Results",
                DefaultExtension = "csv",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } },
                    new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } }
                }
            });

            if (file != null)
            {
                StatusMessage = "Exporting...";
                var fileName = file.Name.ToLowerInvariant();
                
                if (fileName.EndsWith(".json"))
                {
                    await ExportToJsonAsync(Nodes[0], file);
                }
                else
                {
                    await Task.Run(async () =>
                    {
                        using var stream = await file.OpenWriteAsync();
                        using var writer = new System.IO.StreamWriter(stream);
                        await writer.WriteLineAsync("Name,Path,Size (Bytes),Size (Formatted),Type");
                        
                        await ExportNodeRecursive(Nodes[0], writer);
                    });
                }
                StatusMessage = "Export complete.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            Serilog.Log.Error(ex, "Export failed");
        }
    }

    private async Task ExportNodeRecursive(FileSystemNode node, System.IO.StreamWriter writer)
    {
        var type = node.IsDirectory ? "Directory" : "File";
        var line = $"\"{node.Name}\",\"{node.FullPath}\",{node.TotalSize},\"{FormatSize(node.TotalSize)}\",{type}";
        await writer.WriteLineAsync(line);

        foreach (var child in node.Children)
        {
            await ExportNodeRecursive(child, writer);
        }
    }
    
    private async Task ExportToJsonAsync(FileSystemNode node, IStorageFile file)
    {
        await Task.Run(async () =>
        {
            var jsonObject = BuildJsonObject(node);
            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            
            using var stream = await file.OpenWriteAsync();
            await System.Text.Json.JsonSerializer.SerializeAsync(stream, jsonObject, options);
        });
    }
    
    private object BuildJsonObject(FileSystemNode node)
    {
        if (node.IsDirectory)
        {
            return new
            {
                name = node.Name,
                path = node.FullPath,
                type = "directory",
                size = node.TotalSize,
                sizeFormatted = FormatSize(node.TotalSize),
                itemCount = node.ItemCount,
                children = node.Children.Select(c => BuildJsonObject(c)).ToArray()
            };
        }
        else
        {
            return new
            {
                name = node.Name,
                path = node.FullPath,
                type = "file",
                size = node.TotalSize,
                sizeFormatted = FormatSize(node.TotalSize)
            };
        }
    }

    private async Task ExportNodeAsync(FileSystemNode? node)
    {
        if (node == null) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(
                (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
            
            if (topLevel == null) return;

            var suggestedName = $"{node.Name}_export.csv";
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = $"Export {node.Name}",
                SuggestedFileName = suggestedName,
                DefaultExtension = "csv",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } },
                    new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } }
                }
            });

            if (file != null)
            {
                StatusMessage = $"Exporting {node.Name}...";
                var fileName = file.Name.ToLowerInvariant();
                
                if (fileName.EndsWith(".json"))
                {
                    await ExportToJsonAsync(node, file);
                }
                else
                {
                    await Task.Run(async () =>
                    {
                        using var stream = await file.OpenWriteAsync();
                        using var writer = new System.IO.StreamWriter(stream);
                        await writer.WriteLineAsync("Name,Path,Size (Bytes),Size (Formatted),Type");
                        await ExportNodeRecursive(node, writer);
                    });
                }
                StatusMessage = $"Export of {node.Name} complete.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            Serilog.Log.Error(ex, "Export node failed");
        }
    }

    private async Task PerformSearchAsync()
    {
        if (Nodes.Count == 0 || Nodes[0] == null) return;
        
        var query = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            IsSearchActive = false;
            return;
        }

        IsSearchActive = true;
        StatusMessage = "Searching...";
        SearchResultsStatus = "Searching...";
        
        await Task.Run(() =>
        {
            var matches = new List<FileSystemNode>();
            SearchRecursive(Nodes[0], query, matches);
            
            Dispatcher.UIThread.Post(() =>
            {
                SearchResults.Clear();
                foreach (var match in matches.Take(100)) // Limit for performance
                {
                    SearchResults.Add(match);
                }
                var displayCount = Math.Min(matches.Count, 100);
                SearchResultsStatus = matches.Count > 100 
                    ? $"Showing {displayCount} of {matches.Count} matches (double-click to navigate)"
                    : $"Found {matches.Count} matches (double-click to navigate)";
                StatusMessage = $"Found {matches.Count} matches.";
            });
        });
    }

    private void SearchRecursive(FileSystemNode node, string query, List<FileSystemNode> matches)
    {
        if (node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            matches.Add(node);
        }

        foreach (var child in node.Children)
        {
            SearchRecursive(child, query, matches);
        }
    }

    private async Task ScanAsync()
    {
        if (_scanPaths.Count == 0) return;

        // Cancel any ongoing duplicate scan
        _duplicateScanCts?.Cancel();
        
        IsScanning = true;
        StatusMessage = "Scanning...";
        Nodes.Clear();
        PieSeries = Array.Empty<ISeries>();
        BarSeries = Array.Empty<ISeries>();
        TopFiles.Clear();
        DuplicateGroups.Clear();
        DuplicatesStatus = "Click 'Find Duplicates' to scan for duplicate files";

        _scanner = new DiskScanner(SettingsService.Settings?.ScanOptions);
        _scanner.CurrentDirectoryChanged += (s, e) => 
        {
             Dispatcher.UIThread.Post(() => StatusMessage = $"Scanning: {e}");
        };

        try
        {
            long totalItems = 0;
            
            if (_scanPaths.Count == 1)
            {
                // Single path - scan normally
                var result = await Task.Run(() => _scanner.ScanAsync(_scanPaths[0]));
                
                if (result.Root != null)
                {
                    Nodes.Add(result.Root);
                    SelectedNode = result.Root;
                    totalItems = result.Root.ItemCount;
                }
                else
                {
                    StatusMessage = $"Scan failed: {result.ErrorMessage}";
                    HasResults = false;
                    return;
                }
            }
            else
            {
                // Multiple paths - create a virtual root node containing all scanned folders
                var virtualRoot = new FileSystemNode
                {
                    Name = "All Scans",
                    FullPath = "[Multiple Folders]",
                    IsDirectory = true
                };
                
                foreach (var path in _scanPaths)
                {
                    StatusMessage = $"Scanning: {path}";
                    _scanner = new DiskScanner(SettingsService.Settings?.ScanOptions);
                    _scanner.CurrentDirectoryChanged += (s, e) => 
                    {
                         Dispatcher.UIThread.Post(() => StatusMessage = $"Scanning: {e}");
                    };
                    
                    var result = await Task.Run(() => _scanner.ScanAsync(path));
                    
                    if (result.Root != null)
                    {
                        result.Root.Parent = virtualRoot;
                        virtualRoot.Children.Add(result.Root);
                        virtualRoot.TotalSize += result.Root.TotalSize;
                        totalItems += result.Root.ItemCount;
                    }
                }
                
                if (virtualRoot.Children.Count > 0)
                {
                    Nodes.Add(virtualRoot);
                    SelectedNode = virtualRoot;
                }
                else
                {
                    StatusMessage = "No folders could be scanned.";
                    HasResults = false;
                    return;
                }
            }
            
            // Calculate total file count (recursive)
            long totalFileCount = SelectedNode?.TotalItemCount ?? totalItems;
            
            StatusMessage = $"Scan complete. Found {totalFileCount:N0} items.";
            HasResults = true;
            
            // Track scan completion for telemetry
            if (SelectedNode != null)
            {
                _ = TelemetryService.TrackScanCompletedAsync(SelectedNode.TotalSize, totalFileCount);
            }
            
            // Save the paths if "Remember Last Path" is enabled
            if (SettingsService.Settings?.RememberLastPath == true)
            {
                SettingsService.Settings.LastScanPath = string.Join(";", _scanPaths);
                SettingsService.Save();
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan canceled.";
            HasResults = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            HasResults = false;
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task GenerateChartsAsync(FileSystemNode root)
    {
        try
        {
            Log.Information("Generating charts for {Path}", root.FullPath);
            await Task.Run(() =>
            {
                // 1. Folder Contents (Pie Chart)
                // Smart grouping algorithm - balance between showing detail and readability
                var allItems = root.Children.OrderByDescending(x => x.TotalSize).ToList();
                long totalSize = allItems.Sum(x => x.TotalSize);
                
                var pieItems = new List<FileSystemNode>();
                var otherItems = new List<FileSystemNode>();
                
                if (allItems.Any())
                {
                    // Use settings for chart configuration
                    var settings = SettingsService.Settings;
                    int maxSlicesFromSettings = settings?.MaxPieSlices ?? 10;
                    double minPercentForSlice = (settings?.MinSlicePercentage ?? 2.0) / 100.0;
                    
                    // Limit to 18 items max to reserve 1 slot for "Other" (19 total legend items max)
                    int maxSlices = Math.Min(maxSlicesFromSettings, 18);
                    
                    // Algorithm: Show up to MaxSlices items, respecting MinPercentage threshold
                    // Items below threshold go to "Other" unless we have room
                    foreach (var item in allItems)
                    {
                        double percentOfTotal = totalSize > 0 ? (double)item.TotalSize / totalSize : 0;
                        
                        // Add to pie if we have room AND (item is significant OR we haven't filled min slots)
                        bool isSignificant = percentOfTotal >= minPercentForSlice;
                        bool hasRoom = pieItems.Count < maxSlices;
                        
                        if (hasRoom && (isSignificant || pieItems.Count < Math.Min(maxSlices, allItems.Count)))
                        {
                            pieItems.Add(item);
                        }
                        else
                        {
                            otherItems.Add(item);
                        }
                    }
                }
                
                var otherSize = otherItems.Sum(x => x.TotalSize);
                
                // Create virtual "Other" node for drill-down
                FileSystemNode? otherVirtualNode = null;
                if (otherItems.Any())
                {
                    otherVirtualNode = new FileSystemNode
                    {
                        Name = "Other",
                        FullPath = root.FullPath + "\\[Other smaller items]",
                        IsDirectory = true,
                        Parent = root,
                        TotalSize = otherSize,
                        Children = otherItems
                    };
                }
                
                var pieSeriesList = new List<ISeries>();
                
                int pieIndex = 0;
                foreach (var item in pieItems)
                {
                    var color = GetColorForIndex(pieIndex++, pieItems.Count);
                    var itemName = item.Name;
                    var itemSize = item.TotalSize;
                    var itemPercent = totalSize > 0 ? (double)itemSize / totalSize * 100 : 0;
                    var itemCount = item.TotalItemCount;
                    var isDir = item.IsDirectory;
                    
                    // Only show label on slice if it's large enough (> 8%) to avoid overlap
                    // Smaller slices will show info on hover via tooltip
                    bool showLabel = itemPercent >= 8;
                    
                    pieSeriesList.Add(new PieSeries<long>
                    {
                        Values = new[] { itemSize },
                        Name = itemName,
                        DataLabelsPaint = showLabel ? new SolidColorPaint(SKColors.White) : null,
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                        DataLabelsFormatter = showLabel ? (point => $"{itemName}: {FormatSize(point.Coordinate.PrimaryValue)}") : null,
                        Fill = new SolidColorPaint(color),
                        ToolTipLabelFormatter = point => 
                        {
                            var tooltip = $"{FormatSize(itemSize)} ({itemPercent:0.#}%)";
                            if (isDir && itemCount > 0)
                            {
                                tooltip += $" • {itemCount:N0} items";
                            }
                            return tooltip;
                        }
                    });
                }

                if (otherVirtualNode != null)
                {
                    var otherPercent = totalSize > 0 ? (double)otherSize / totalSize * 100 : 0;
                    bool showOtherLabel = otherPercent >= 8;
                    
                    pieSeriesList.Add(new PieSeries<long>
                    {
                        Values = new[] { otherSize },
                        Name = "Other",
                        DataLabelsPaint = showOtherLabel ? new SolidColorPaint(SKColors.White) : null,
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                        DataLabelsFormatter = showOtherLabel ? (point => $"Other: {FormatSize(point.Coordinate.PrimaryValue)}") : null,
                        Fill = new SolidColorPaint(SKColors.Gray),
                        ToolTipLabelFormatter = point => 
                        {
                            return $"{FormatSize(otherSize)} ({otherPercent:0.#}%) • {otherItems.Count} items";
                        }
                    });
                }
                
                // Store virtual node for click handling
                _otherVirtualNode = otherVirtualNode;

                var pieSeries = pieSeriesList.ToArray();

                // 2. Top Items (Bar Chart - Horizontal)
                // Use the SAME list as Pie Chart (allItems) to ensure consistency
                var maxBarItems = SettingsService.Settings?.MaxBarItems ?? 15;
                var topItems = allItems.Take(maxBarItems).Reverse().ToList();
                _currentBarItems = topItems;
                
                // Determine text color based on theme - check actual applied theme variant for reliability
                bool isDarkMode;
                var themeSetting = SettingsService.Settings?.Theme;
                if (themeSetting == "Dark")
                {
                    isDarkMode = true;
                }
                else if (themeSetting == "Light")
                {
                    isDarkMode = false;
                }
                else
                {
                    // Auto or unknown - check actual theme variant
                    isDarkMode = global::Avalonia.Application.Current?.ActualThemeVariant == global::Avalonia.Styling.ThemeVariant.Dark;
                }
                var textColor = isDarkMode ? SKColors.White : SKColors.Black;
                var secondaryTextColor = isDarkMode ? new SKColor(180, 180, 180) : new SKColor(80, 80, 80);
                
                // Use a single RowSeries with all values
                // Capture topItems and totalSize for tooltip closure
                var barTopItems = topItems;
                var barTotalSize = totalSize;
                var barSeries = new RowSeries<long>
                {
                    Values = topItems.Select(x => x.TotalSize).ToArray(),
                    Name = "Size",
                    Fill = new SolidColorPaint(SKColor.Parse("#4CAF50")),
                    Stroke = null,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                    DataLabelsFormatter = point => FormatSize(point.Coordinate.PrimaryValue),
                    MaxBarWidth = 50,
                    // Custom tooltip - hide the default X value (which shows "6 B", "7 B", etc.)
                    XToolTipLabelFormatter = point => string.Empty,
                    YToolTipLabelFormatter = point =>
                    {
                        var idx = point.Index;
                        if (idx >= 0 && idx < barTopItems.Count)
                        {
                            var item = barTopItems[idx];
                            var pct = barTotalSize > 0 ? (double)item.TotalSize / barTotalSize * 100 : 0;
                            return $"{item.Name}: {FormatSize(item.TotalSize)} ({pct:0.#}%)";
                        }
                        return FormatSize(point.Coordinate.PrimaryValue);
                    }
                };

                var yAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = topItems.Select(x => x.Name).ToArray(),
                        LabelsPaint = new SolidColorPaint(textColor),
                        MinLimit = -0.5,
                        MaxLimit = topItems.Count - 0.5
                    }
                };
                
                var xAxes = new Axis[]
                {
                    new Axis
                    {
                        Labeler = value => FormatSize((long)value),
                        LabelsPaint = new SolidColorPaint(secondaryTextColor),
                        MinLimit = 0
                    }
                };

                // 3. Top Files List - use settings for max count
                var maxTopFiles = SettingsService.Settings?.MaxTopFiles ?? 100;
                var topFilesList = _analysisService.GetTopFiles(root, maxTopFiles);

                Dispatcher.UIThread.Post(() =>
                {
                    PieSeries = pieSeries;
                    BarSeries = new ISeries[] { barSeries };
                    BarXAxes = xAxes;
                    BarYAxes = yAxes;
                    
                    TopFiles.Clear();
                    foreach (var file in topFilesList)
                    {
                        TopFiles.Add(file);
                    }
                    
                    // Generate File Type Analysis
                    GenerateFileTypeAnalysis(root);
                });
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => StatusMessage = $"Error generating charts: {ex.Message}");
        }
    }
    
    private void GenerateFileTypeAnalysis(FileSystemNode root)
    {
        var allFiles = new List<FileSystemNode>();
        CollectFiles(root, allFiles);
        
        var grouped = allFiles
            .GroupBy(f => System.IO.Path.GetExtension(f.Name).ToLowerInvariant())
            .Select(g => new FileTypeInfo
            {
                Extension = string.IsNullOrEmpty(g.Key) ? "(no extension)" : g.Key,
                Count = g.Count(),
                TotalSize = g.Sum(f => f.TotalSize)
            })
            .OrderByDescending(x => x.TotalSize)
            .ToList();
        
        var grandTotal = grouped.Sum(x => x.TotalSize);
        foreach (var item in grouped)
        {
            item.Percentage = grandTotal > 0 ? (double)item.TotalSize / grandTotal * 100 : 0;
        }
        
        FileTypeAnalysis.Clear();
        foreach (var item in grouped.Take(50)) // Top 50 file types
        {
            FileTypeAnalysis.Add(item);
        }
    }
    
    private void CollectFiles(FileSystemNode node, List<FileSystemNode> files)
    {
        if (!node.IsDirectory)
        {
            files.Add(node);
        }
        foreach (var child in node.Children)
        {
            CollectFiles(child, files);
        }
    }
    
    private async Task FindDuplicatesAsync()
    {
        if (Nodes.Count == 0 || Nodes[0] == null) return;
        
        // Cancel any existing duplicate scan
        _duplicateScanCts?.Cancel();
        _duplicateScanCts = new CancellationTokenSource();
        var ct = _duplicateScanCts.Token;
        
        IsDuplicateScanning = true;
        DuplicatesStatus = "Collecting files...";
        DuplicateGroups.Clear();
        NeedsVerification = false;
        
        try
        {
            var (duplicates, isHdd) = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                
                var allFiles = new List<FileSystemNode>();
                
                // Collect files from all scanned nodes - take a snapshot of current nodes
                var currentNodes = Nodes.ToList();
                foreach (var rootNode in currentNodes)
                {
                    if (rootNode != null)
                    {
                        CollectFiles(rootNode, allFiles);
                    }
                }
                
                ct.ThrowIfCancellationRequested();
                
                // Deduplicate by path
                var uniqueFiles = allFiles
                    .GroupBy(f => f.FullPath.ToLowerInvariant())
                    .Select(g => g.First())
                    .ToList();
                
                Dispatcher.UIThread.Post(() => DuplicatesStatus = $"Grouping {uniqueFiles.Count:N0} files...");
                
                const long MinFileSize = 1024; // 1 KB minimum
                
                // Group by NAME + SIZE (exact match required)
                var nameSizeGroups = uniqueFiles
                    .Where(f => f.TotalSize >= MinFileSize)
                    .GroupBy(f => (Name: f.Name.ToLowerInvariant(), Size: f.TotalSize))
                    .Where(g => g.Count() > 1)
                    .ToList();
                
                // Detect if any scanned paths are on HDD (spinning disk) - cross-platform
                var hasHDD = currentNodes.Any(n => IsHardDiskDriveCrossPlatform(n.FullPath));
                
                var result = new List<DuplicateGroup>();
                
                if (hasHDD)
                {
                    // HDD ZERO-READ MODE: Use metadata fingerprint (no disk reads!)
                    // Group by: name + size + creation time + modified time
                    // This is INSTANT because all metadata is already in memory from the scan
                    int totalGroups = nameSizeGroups.Count;
                    
                    Dispatcher.UIThread.Post(() => DuplicatesStatus = $"HDD detected - metadata fingerprint ({totalGroups:N0} groups, zero disk reads)...");
                    
                    foreach (var group in nameSizeGroups)
                    {
                        ct.ThrowIfCancellationRequested();
                        
                        var filesInGroup = group.ToList();
                        
                        // Sub-group by metadata fingerprint: creation time + modified time
                        // Files with same name + size + timestamps are almost certainly identical
                        var metadataGroups = filesInGroup
                            .GroupBy(f => (
                                Created: f.CreationTime.Ticks / TimeSpan.TicksPerSecond, // Round to second
                                Modified: f.LastModified.Ticks / TimeSpan.TicksPerSecond
                            ))
                            .Where(g => g.Count() > 1);
                        
                        foreach (var metaGroup in metadataGroups)
                        {
                            var files = metaGroup.ToList();
                            var uniquePaths = files
                                .Select(f => f.FullPath.ToLowerInvariant())
                                .Distinct()
                                .ToList();
                            
                            if (uniquePaths.Count > 1)
                            {
                                // Create a fingerprint hash from metadata (no file reading)
                                var fingerprint = $"META:{files[0].TotalSize}:{metaGroup.Key.Created}:{metaGroup.Key.Modified}";
                                
                                result.Add(new DuplicateGroup
                                {
                                    Size = files[0].TotalSize,
                                    Files = files,
                                    Hash = fingerprint,
                                    IsVerified = false // Metadata-only, needs verification to be sure
                                });
                            }
                        }
                    }
                }
                else
                {
                    // SSD MODE: Full parallel hashing
                    var bag = new System.Collections.Concurrent.ConcurrentBag<DuplicateGroup>();
                    int processedGroups = 0;
                    int totalGroups = nameSizeGroups.Count;
                    var parallelism = Environment.ProcessorCount * 2;
                    
                    Dispatcher.UIThread.Post(() => DuplicatesStatus = $"Hashing {totalGroups:N0} groups (SSD parallel mode)...");
                    
                    try
                    {
                        Parallel.ForEach(nameSizeGroups, 
                            new ParallelOptions 
                            { 
                                CancellationToken = ct, 
                                MaxDegreeOfParallelism = parallelism
                            },
                            (nameSizeGroup, loopState) =>
                        {
                            if (ct.IsCancellationRequested)
                            {
                                loopState.Stop();
                                return;
                            }
                            
                            var count = Interlocked.Increment(ref processedGroups);
                            if (count % 500 == 0)
                            {
                                Dispatcher.UIThread.Post(() => DuplicatesStatus = $"Hashing {count:N0}/{totalGroups:N0}...");
                            }
                            
                            var filesInGroup = nameSizeGroup.ToList();
                            var quickHashGroups = new Dictionary<string, List<FileSystemNode>>();
                        
                            foreach (var file in filesInGroup)
                            {
                                if (ct.IsCancellationRequested)
                                {
                                    loopState.Stop();
                                    return;
                                }
                                
                                try
                                {
                                    if (!System.IO.File.Exists(file.FullPath)) continue;
                                    
                                    var quickHash = ComputeQuickHash(file.FullPath, file.TotalSize);
                                    if (quickHash == null) continue;
                                    
                                    if (!quickHashGroups.ContainsKey(quickHash))
                                        quickHashGroups[quickHash] = new List<FileSystemNode>();
                                    quickHashGroups[quickHash].Add(file);
                                }
                                catch { /* Skip inaccessible files */ }
                            }
                            
                            foreach (var quickGroup in quickHashGroups.Where(g => g.Value.Count > 1))
                            {
                                var uniquePaths = quickGroup.Value
                                    .Select(f => f.FullPath.ToLowerInvariant())
                                    .Distinct()
                                    .ToList();
                                
                                if (uniquePaths.Count > 1)
                                {
                                    bag.Add(new DuplicateGroup
                                    {
                                        Size = quickGroup.Value[0].TotalSize,
                                        Files = quickGroup.Value,
                                        Hash = quickGroup.Key,
                                        IsVerified = true
                                    });
                                }
                            }
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    
                    result = bag.ToList();
                }
                
                Dispatcher.UIThread.Post(() => DuplicatesStatus = $"Sorting {result.Count:N0} duplicate groups...");
                
                return (result.OrderByDescending(d => d.WastedSpace).ToList(), hasHDD);
            }, ct);
            
            HasHddDrives = isHdd;
            NeedsVerification = isHdd && duplicates.Count > 0; // HDD uses metadata-only, needs verification
            
            var totalWasted = duplicates.Sum(d => d.WastedSpace);
            
            DuplicateGroups.Clear();
            foreach (var group in duplicates.Take(500))
            {
                DuplicateGroups.Add(group);
            }
            
            if (isHdd)
            {
                DuplicatesStatus = duplicates.Count > 0 
                    ? $"⚡ Found {duplicates.Count:N0} likely duplicates ({FormatSize(totalWasted)} potential savings) - Click 'Verify' to confirm"
                    : "No duplicates found (files with same name + size + timestamps)";
            }
            else
            {
                DuplicatesStatus = duplicates.Count > 0 
                    ? $"✓ Found {duplicates.Count:N0} verified duplicates, {FormatSize(totalWasted)} wasted"
                    : "No duplicates found (files with same name + size > 1KB)";
            }
            
            // Track duplicate scan for telemetry
            _ = TelemetryService.TrackDuplicateScanAsync(duplicates.Count, totalWasted);
        }
        catch (OperationCanceledException)
        {
            DuplicatesStatus = "Duplicate scan cancelled.";
            DuplicateGroups.Clear();
            NeedsVerification = false;
        }
        catch (Exception ex)
        {
            DuplicatesStatus = $"Error: {ex.Message}";
            Log.Error(ex, "Error finding duplicates");
        }
        finally
        {
            IsDuplicateScanning = false;
        }
    }
    
    private async Task VerifyDuplicatesAsync()
    {
        if (DuplicateGroups.Count == 0) return;
        
        _duplicateScanCts?.Cancel();
        _duplicateScanCts = new CancellationTokenSource();
        var ct = _duplicateScanCts.Token;
        
        IsDuplicateScanning = true;
        var groupsToVerify = DuplicateGroups.Where(g => !g.IsVerified).ToList();
        DuplicatesStatus = $"Verifying {groupsToVerify.Count:N0} groups (this may take a while on HDD)...";
        
        try
        {
            var verifiedGroups = await Task.Run(() =>
            {
                var result = new System.Collections.Concurrent.ConcurrentBag<DuplicateGroup>();
                int processed = 0;
                int total = groupsToVerify.Count;
                int totalFiles = groupsToVerify.Sum(g => g.Files.Count);
                int filesProcessed = 0;
                
                // Sequential for HDD to minimize seeking, sorted by path
                // First, collect all files and sort globally by path for optimal disk access
                var allFilesWithGroup = groupsToVerify
                    .SelectMany(g => g.Files.Select(f => (File: f, Group: g)))
                    .OrderBy(x => x.File.FullPath)
                    .ToList();
                
                var fileHashes = new Dictionary<FileSystemNode, string>();
                
                Dispatcher.UIThread.Post(() => DuplicatesStatus = $"Reading {totalFiles:N0} files (sorted for HDD)...");
                
                // First pass: compute hashes in disk order
                foreach (var item in allFilesWithGroup)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    filesProcessed++;
                    if (filesProcessed % 100 == 0)
                    {
                        var fp = filesProcessed;
                        Dispatcher.UIThread.Post(() => DuplicatesStatus = $"Hashing {fp:N0}/{totalFiles:N0} files...");
                    }
                    
                    try
                    {
                        if (!System.IO.File.Exists(item.File.FullPath)) continue;
                        
                        // Use fast HDD hash (512 bytes sample-based)
                        var hash = ComputeHddFastHash(item.File.FullPath, item.File.TotalSize);
                        if (hash != null)
                        {
                            fileHashes[item.File] = hash;
                        }
                    }
                    catch { /* Skip inaccessible files */ }
                }
                
                Dispatcher.UIThread.Post(() => DuplicatesStatus = $"Grouping verified duplicates...");
                
                // Second pass: group by original group and hash
                foreach (var group in groupsToVerify)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    var hashGroups = group.Files
                        .Where(f => fileHashes.ContainsKey(f))
                        .GroupBy(f => fileHashes[f])
                        .Where(g => g.Count() > 1);
                    
                    foreach (var hashGroup in hashGroups)
                    {
                        var files = hashGroup.ToList();
                        var uniquePaths = files
                            .Select(f => f.FullPath.ToLowerInvariant())
                            .Distinct()
                            .ToList();
                        
                        if (uniquePaths.Count > 1)
                        {
                            result.Add(new DuplicateGroup
                            {
                                Size = files[0].TotalSize,
                                Files = files,
                                Hash = hashGroup.Key,
                                IsVerified = true
                            });
                        }
                    }
                }
                
                return result.OrderByDescending(d => d.WastedSpace).ToList();
            }, ct);
            
            var totalWasted = verifiedGroups.Sum(d => d.WastedSpace);
            
            DuplicateGroups.Clear();
            foreach (var group in verifiedGroups.Take(500))
            {
                DuplicateGroups.Add(group);
            }
            
            NeedsVerification = false;
            DuplicatesStatus = verifiedGroups.Count > 0 
                ? $"✓ Verified {verifiedGroups.Count:N0} true duplicates, {FormatSize(totalWasted)} wasted"
                : "No true duplicates found after verification";
        }
        catch (OperationCanceledException)
        {
            DuplicatesStatus = "Verification cancelled.";
        }
        catch (Exception ex)
        {
            DuplicatesStatus = $"Verification error: {ex.Message}";
            Log.Error(ex, "Error verifying duplicates");
        }
        finally
        {
            IsDuplicateScanning = false;
        }
    }
    
    private void CancelDuplicateScan()
    {
        _duplicateScanCts?.Cancel();
    }
    
    // Fast hash using first and last bytes + file size (for SSD)
    private string? ComputeQuickHash(string filePath, long fileSize)
    {
        try
        {
            const int sampleSize = 4096; // 4KB samples
            
            using var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, 
                System.IO.FileAccess.Read, System.IO.FileShare.Read, bufferSize: 8192);
            
            // For very small files, just hash everything
            if (fileSize <= sampleSize * 2)
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(stream));
            }
            
            // Read first 4KB
            var startBuffer = new byte[sampleSize];
            int startRead = stream.Read(startBuffer, 0, sampleSize);
            
            // Read last 4KB
            stream.Seek(-sampleSize, System.IO.SeekOrigin.End);
            var endBuffer = new byte[sampleSize];
            int endRead = stream.Read(endBuffer, 0, sampleSize);
            
            // Combine: size (8 bytes) + start bytes + end bytes
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            sha256.TransformBlock(BitConverter.GetBytes(fileSize), 0, 8, null, 0);
            sha256.TransformBlock(startBuffer, 0, startRead, null, 0);
            sha256.TransformFinalBlock(endBuffer, 0, endRead);
            
            return Convert.ToHexString(sha256.Hash!);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Ultra-fast sample-based hash for HDD drives.
    /// Reads only 512 bytes total (8 samples of 64 bytes at fixed positions).
    /// For files with same name+size, matching samples = >99.99% identical.
    /// This is ~16x faster than ComputeQuickHash on HDD due to minimal seeks.
    /// </summary>
    private string? ComputeHddFastHash(string filePath, long fileSize)
    {
        try
        {
            const int sampleCount = 8;      // Number of sample points
            const int sampleSize = 64;      // Bytes per sample (one disk sector read)
            const int totalSampleBytes = sampleCount * sampleSize; // 512 bytes total
            
            using var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, 
                System.IO.FileAccess.Read, System.IO.FileShare.Read, 
                bufferSize: 4096, System.IO.FileOptions.RandomAccess);
            
            // For very small files, just hash everything
            if (fileSize <= totalSampleBytes)
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(stream));
            }
            
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            
            // Include file size in hash
            sha256.TransformBlock(BitConverter.GetBytes(fileSize), 0, 8, null, 0);
            
            var buffer = new byte[sampleSize];
            
            // Sample at fixed percentage positions: 0%, 12.5%, 25%, 37.5%, 50%, 62.5%, 75%, 87.5%
            // Plus we always read the last 64 bytes
            for (int i = 0; i < sampleCount - 1; i++)
            {
                long position = (fileSize * i) / (sampleCount - 1);
                // Ensure we don't read past end of file
                position = Math.Min(position, fileSize - sampleSize);
                
                stream.Seek(position, System.IO.SeekOrigin.Begin);
                int bytesRead = stream.Read(buffer, 0, sampleSize);
                
                if (bytesRead > 0)
                {
                    sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                }
            }
            
            // Always read last 64 bytes
            stream.Seek(-sampleSize, System.IO.SeekOrigin.End);
            int lastRead = stream.Read(buffer, 0, sampleSize);
            sha256.TransformFinalBlock(buffer, 0, lastRead);
            
            return Convert.ToHexString(sha256.Hash!);
        }
        catch
        {
            return null;
        }
    }

    private string FormatSize(double bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        while (bytes >= 1024 && order < sizes.Length - 1)
        {
            order++;
            bytes /= 1024;
        }
        return $"{bytes:0.##} {sizes[order]}";
    }
    
    // Duplicate deletion strategies
    public enum DuplicateKeepStrategy
    {
        KeepNewest,
        KeepOldest,
        KeepMostRecentAccess,
        KeepShortestPath,
        KeepLongestPath
    }
    
    public async Task DeleteDuplicatesAsync(IEnumerable<DuplicateGroup> groups, DuplicateKeepStrategy strategy)
    {
        var groupList = groups.ToList();
        if (groupList.Count == 0) return;
        
        int deletedCount = 0;
        long savedSpace = 0;
        var errors = new List<string>();
        
        StatusMessage = "Deleting duplicates...";
        
        await Task.Run(() =>
        {
            foreach (var group in groupList)
            {
                try
                {
                    // Determine which file to keep based on strategy
                    var fileInfos = group.Files
                        .Select(f => 
                        {
                            try
                            {
                                return new { Node = f, Info = new System.IO.FileInfo(f.FullPath) };
                            }
                            catch
                            {
                                return null;
                            }
                        })
                        .Where(x => x != null && x.Info.Exists)
                        .ToList();
                    
                    if (fileInfos.Count < 2) continue;
                    
                    FileSystemNode? fileToKeep = strategy switch
                    {
                        DuplicateKeepStrategy.KeepNewest => fileInfos
                            .OrderByDescending(f => f!.Info.LastWriteTimeUtc)
                            .First()?.Node,
                        DuplicateKeepStrategy.KeepOldest => fileInfos
                            .OrderBy(f => f!.Info.LastWriteTimeUtc)
                            .First()?.Node,
                        DuplicateKeepStrategy.KeepMostRecentAccess => fileInfos
                            .OrderByDescending(f => f!.Info.LastAccessTimeUtc)
                            .First()?.Node,
                        DuplicateKeepStrategy.KeepShortestPath => fileInfos
                            .OrderBy(f => f!.Node.FullPath.Length)
                            .First()?.Node,
                        DuplicateKeepStrategy.KeepLongestPath => fileInfos
                            .OrderByDescending(f => f!.Node.FullPath.Length)
                            .First()?.Node,
                        _ => fileInfos.First()?.Node
                    };
                    
                    if (fileToKeep == null) continue;
                    
                    // Delete all other files
                    foreach (var fileInfo in fileInfos)
                    {
                        if (fileInfo?.Node.FullPath != fileToKeep.FullPath)
                        {
                            try
                            {
                                System.IO.File.Delete(fileInfo!.Node.FullPath);
                                savedSpace += fileInfo.Node.TotalSize;
                                deletedCount++;
                                
                                // Remove from parent in the tree
                                if (fileInfo.Node.Parent != null)
                                {
                                    Dispatcher.UIThread.Post(() =>
                                    {
                                        fileInfo.Node.Parent.Children.Remove(fileInfo.Node);
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                errors.Add($"{fileInfo.Node.Name}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Group error: {ex.Message}");
                }
            }
        });
        
        // Remove processed groups from the list
        foreach (var group in groupList)
        {
            DuplicateGroups.Remove(group);
        }
        
        var errorSuffix = errors.Count > 0 ? $" ({errors.Count} errors)" : "";
        StatusMessage = $"Deleted {deletedCount} duplicate files, freed {FormatSize(savedSpace)}{errorSuffix}";
        DuplicatesStatus = $"Deleted {deletedCount} files. Remaining: {DuplicateGroups.Count} groups.";
        
        if (errors.Count > 0)
        {
            Log.Warning("Errors deleting duplicates: {Errors}", string.Join("; ", errors.Take(10)));
        }
    }
    
    /// <summary>
    /// Cross-platform HDD detection. Returns true for HDD, false for SSD/Unknown.
    /// </summary>
    private static bool IsHardDiskDriveCrossPlatform(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return IsHardDiskDriveWindows(path);
            }
            else if (OperatingSystem.IsLinux())
            {
                return IsHardDiskDriveLinux(path);
            }
            else if (OperatingSystem.IsMacOS())
            {
                return IsHardDiskDriveMacOS(path);
            }
        }
        catch (Exception ex)
        {
            Log.Debug("Could not detect drive type for {Path}: {Error}", path, ex.Message);
        }
        
        return false; // Assume SSD if detection fails
    }
    
    /// <summary>
    /// Linux HDD detection using /sys/block/*/queue/rotational
    /// </summary>
    private static bool IsHardDiskDriveLinux(string path)
    {
        try
        {
            // Get the mount point for this path
            var fullPath = System.IO.Path.GetFullPath(path);
            
            // Read /proc/mounts to find the device for this path
            if (!System.IO.File.Exists("/proc/mounts")) return false;
            
            var mounts = System.IO.File.ReadAllLines("/proc/mounts");
            string? deviceName = null;
            int longestMatch = 0;
            
            foreach (var line in mounts)
            {
                var parts = line.Split(' ');
                if (parts.Length < 2) continue;
                
                var device = parts[0];
                var mountPoint = parts[1];
                
                if (fullPath.StartsWith(mountPoint) && mountPoint.Length > longestMatch)
                {
                    longestMatch = mountPoint.Length;
                    deviceName = device;
                }
            }
            
            if (string.IsNullOrEmpty(deviceName)) return false;
            
            // Extract the base device name (e.g., /dev/sda1 -> sda)
            var baseName = System.IO.Path.GetFileName(deviceName);
            // Remove partition number (sda1 -> sda, nvme0n1p1 -> nvme0n1)
            while (baseName.Length > 0 && char.IsDigit(baseName[^1]))
            {
                baseName = baseName[..^1];
            }
            // Handle nvme devices (nvme0n1p -> nvme0n1)
            if (baseName.EndsWith("p"))
            {
                baseName = baseName[..^1];
            }
            
            // Check /sys/block/{device}/queue/rotational
            // 1 = HDD (rotational), 0 = SSD (non-rotational)
            var rotationalPath = $"/sys/block/{baseName}/queue/rotational";
            if (System.IO.File.Exists(rotationalPath))
            {
                var value = System.IO.File.ReadAllText(rotationalPath).Trim();
                return value == "1";
            }
        }
        catch (Exception ex)
        {
            Log.Debug("Linux HDD detection failed: {Error}", ex.Message);
        }
        
        return false;
    }
    
    /// <summary>
    /// macOS HDD detection using diskutil
    /// </summary>
    private static bool IsHardDiskDriveMacOS(string path)
    {
        try
        {
            // Get the volume for this path
            var fullPath = System.IO.Path.GetFullPath(path);
            
            // Find the mount point
            var driveInfo = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(fullPath) ?? "/");
            
            // Use diskutil to get disk info
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/sbin/diskutil",
                Arguments = $"info \"{driveInfo.Name}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return false;
            
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            
            // Look for "Solid State: Yes/No" or "SSD: Yes/No"
            if (output.Contains("Solid State:"))
            {
                return output.Contains("Solid State:   No");
            }
            
            // Fallback: check if it's a "HDD" in the protocol
            if (output.Contains("Protocol:"))
            {
                // NVMe and USB typically indicate SSD, SATA could be either
                // But if it says "Rotational", it's HDD
                return output.Contains("Rotational:   Yes");
            }
        }
        catch (Exception ex)
        {
            Log.Debug("macOS HDD detection failed: {Error}", ex.Message);
        }
        
        return false;
    }
    
    /// <summary>
    /// Windows HDD detection using PowerShell Get-PhysicalDisk.
    /// Returns true for HDD, false for SSD/NVMe/Unknown.
    /// This avoids the heavyweight System.Management dependency.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool IsHardDiskDriveWindows(string path)
    {
        try
        {
            var driveLetter = System.IO.Path.GetPathRoot(path)?.TrimEnd('\\');
            if (string.IsNullOrEmpty(driveLetter)) return false;
            
            // Use PowerShell to query disk media type
            // MediaType: HDD = spinning, SSD = solid state, Unspecified = unknown
            var script = $@"
                $partition = Get-Partition -DriveLetter '{driveLetter[0]}' -ErrorAction SilentlyContinue
                if ($partition) {{
                    $disk = Get-PhysicalDisk | Where-Object {{ $_.DeviceId -eq $partition.DiskNumber }}
                    if ($disk) {{ $disk.MediaType }}
                }}
            ";
            
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\" ",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process == null) return false;
            
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000); // 5 second timeout
            
            Log.Debug("Drive {DriveLetter} MediaType: {MediaType}", driveLetter, output);
            
            // Check if it's an HDD (spinning disk)
            return output.Equals("HDD", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Debug("Could not detect drive type for {Path}: {Error}", path, ex.Message);
        }
        
        return false; // Assume SSD if detection fails (safer for parallelism)
    }
    
    /// <summary>
    /// Gets a human-readable drive type label for a path (cross-platform).
    /// </summary>
    public static string GetDriveTypeLabel(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return GetDriveTypeLabelWindows(path);
        }
        else if (OperatingSystem.IsLinux())
        {
            return IsHardDiskDriveLinux(path) ? "HDD" : "SSD";
        }
        else if (OperatingSystem.IsMacOS())
        {
            return IsHardDiskDriveMacOS(path) ? "HDD" : "SSD";
        }
        return "Unknown";
    }
    
    /// <summary>
    /// Windows-specific drive type label using PowerShell.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static string GetDriveTypeLabelWindows(string path)
    {
        try
        {
            var driveLetter = System.IO.Path.GetPathRoot(path)?.TrimEnd('\\');
            if (string.IsNullOrEmpty(driveLetter)) return "Unknown";
            
            // Use PowerShell to query disk media type
            var script = $@"
                $partition = Get-Partition -DriveLetter '{driveLetter[0]}' -ErrorAction SilentlyContinue
                if ($partition) {{
                    $disk = Get-PhysicalDisk | Where-Object {{ $_.DeviceId -eq $partition.DiskNumber }}
                    if ($disk) {{ $disk.MediaType }}
                }}
            ";
            
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\" ",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process == null) return "Unknown";
            
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            
            return output switch
            {
                "HDD" => "HDD",
                "SSD" => "SSD",
                "SCM" => "SCM",
                _ => "Unknown"
            };
        }
        catch (Exception ex)
        {
            Log.Debug("Could not detect drive type for {Path}: {Error}", path, ex.Message);
        }
        
        return "Unknown";
    }
}

// Helper classes for File Type Analysis and Duplicate Detection
public class FileTypeInfo
{
    public string Extension { get; set; } = string.Empty;
    public int Count { get; set; }
    public long TotalSize { get; set; }
    public double Percentage { get; set; }
    
    public string FormattedSize => FormatSize(TotalSize);
    public string PercentageFormatted => $"{Percentage:0.#}%";
    
    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public class DuplicateGroup : ReactiveUI.ReactiveObject
{
    public long Size { get; set; }
    public List<FileSystemNode> Files { get; set; } = new();
    public string Hash { get; set; } = string.Empty;
    
    private bool _isVerified;
    public bool IsVerified 
    { 
        get => _isVerified; 
        set => this.RaiseAndSetIfChanged(ref _isVerified, value);
    }
    
    public int Count => Files.Count;
    public long WastedSpace => Size * (Count - 1);
    
    public string FormattedSize => FormatSize(Size);
    public string WastedSpaceFormatted => FormatSize(WastedSpace);
    public string StatusIcon => IsVerified ? "✓" : "?";
    public string FileNames => string.Join(", ", Files.Take(5).Select(f => f.Name)) + (Files.Count > 5 ? $" (+{Files.Count - 5} more)" : "");
    
    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

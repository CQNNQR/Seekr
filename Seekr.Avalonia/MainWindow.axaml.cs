using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using Seekr.Avalonia.ViewModels;
using Seekr.Models;
using Seekr.Core.Services.Abstractions;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Seekr.Avalonia;

public partial class MainWindow : Window
{
    private ContextMenu? _activeContextMenu;
    private DispatcherTimer? _contextMenuTimer;
    private readonly ISettingsService _settingsService;

    public MainWindow(MainWindowViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settingsService = settingsService;

        PieChartControl.AddHandler(PointerPressedEvent, OnPieChartPointerPressed, RoutingStrategies.Tunnel);
        BarChartControl.AddHandler(PointerPressedEvent, OnBarChartPointerPressed, RoutingStrategies.Tunnel);

        PieChartControl.PointerMoved += OnPieChartPointerMoved;
        PieChartControl.PointerExited += OnPieChartPointerExited;

        this.PointerPressed += OnWindowPointerPressed;

        this.KeyDown += OnWindowKeyDown;

        ApplyDefaultTabSelection();

        ShowTelemetryNoticeIfNeeded();
    }

    private async void ShowTelemetryNoticeIfNeeded()
    {
        var settings = _settingsService.Settings;
        if (settings == null || settings.HasShownTelemetryConsent) return;

        settings.HasShownTelemetryConsent = true;
        _settingsService.Save();

        var notice = this.FindControl<Border>("TelemetryNotice");
        if (notice == null) return;

        notice.IsVisible = true;
        notice.Opacity = 0.95;

        await Task.Delay(3000);

        var fadeSteps = 10;
        var stepDelay = 50;
        for (int i = fadeSteps; i >= 0; i--)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                notice.Opacity = (0.95 * i) / fadeSteps;
            });
            await Task.Delay(stepDelay);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            notice.IsVisible = false;
        });
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (e.Key == Key.Back || (e.Key == Key.Left && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            if (vm.NavigateUpCommand.CanExecute(null))
            {
                vm.NavigateUpCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F5)
        {
            if (vm.ScanCommand.CanExecute(null))
            {
                vm.ScanCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            SelectFolderButton_Click(null, null!);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            if (vm.SelectedNode != null && vm.DeleteCommand.CanExecute(vm.SelectedNode))
            {
                vm.DeleteCommand.Execute(vm.SelectedNode);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            if (vm.IsScanning && vm.CancelCommand.CanExecute(null))
            {
                vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.IsSearchActive)
            {
                vm.SearchText = string.Empty;
                e.Handled = true;
            }
        }
    }

    private void ApplyDefaultTabSelection()
    {
        var defaultGraph = _settingsService.Settings?.DefaultGraph ?? "Pie";

        switch (defaultGraph)
        {
            case "Bar":
                ChartTabControl.SelectedItem = BarGraphTab;
                break;
            case "Treemap":
                ChartTabControl.SelectedItem = TreemapTab;
                break;
            case "Pie":
            default:
                ChartTabControl.SelectedItem = PieChartTab;
                break;
        }
    }

    private void OnPieChartPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        CloseActiveContextMenu();

        if (DataContext is not MainWindowViewModel vm) return;

        if (props.IsRightButtonPressed)
        {
            vm.SkipNextChartClick = true;
            e.Handled = true;

            var pos = e.GetPosition(PieChartControl);
            var lvcPoint = new LvcPointD(pos.X, pos.Y);
            var points = PieChartControl.GetPointsAt(lvcPoint);
            var point = points.FirstOrDefault();

            FileSystemNode? targetNode = null;
            if (point != null)
            {
                var name = point.Context?.Series?.Name;
                if (name == "Other")
                {
                    targetNode = vm.GetOtherVirtualNode();
                }
                else if (name != null)
                {
                    targetNode = vm.SelectedNode?.Children.FirstOrDefault(c => c.Name == name);
                }
            }

            if (targetNode != null)
            {
                ShowNodeContextMenu(targetNode, e);
            }
            return;
        }

        if (props.IsLeftButtonPressed)
        {
            vm.SkipNextChartClick = true;

            var pos = e.GetPosition(PieChartControl);
            var lvcPoint = new LvcPointD(pos.X, pos.Y);
            var points = PieChartControl.GetPointsAt(lvcPoint);
            var point = points.FirstOrDefault();

            if (point != null)
            {
                var name = point.Context?.Series?.Name;

                if (name == "Other" && vm.GetOtherVirtualNode() != null)
                {
                    vm.SelectedNode = vm.GetOtherVirtualNode();
                }
                else if (name != null)
                {
                    var node = vm.SelectedNode?.Children.FirstOrDefault(c => c.Name == name);
                    if (node != null && node.IsDirectory)
                    {
                        vm.SelectedNode = node;
                    }
                }
            }
        }
    }

    private void OnBarChartPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        CloseActiveContextMenu();

        if (DataContext is not MainWindowViewModel vm) return;

        if (props.IsRightButtonPressed)
        {
            vm.SkipNextChartClick = true;
            e.Handled = true;

            var pos = e.GetPosition(BarChartControl);
            var lvcPoint = new LvcPointD(pos.X, pos.Y);
            var points = BarChartControl.GetPointsAt(lvcPoint);
            var point = points.FirstOrDefault();

            if (point != null)
            {
                var targetNode = vm.GetBarItemAtIndex(point.Index);
                if (targetNode != null)
                {
                    ShowNodeContextMenu(targetNode, e);
                }
            }
            return;
        }

        if (props.IsLeftButtonPressed)
        {
            vm.SkipNextChartClick = true;

            var pos = e.GetPosition(BarChartControl);
            var lvcPoint = new LvcPointD(pos.X, pos.Y);
            var points = BarChartControl.GetPointsAt(lvcPoint);
            var point = points.FirstOrDefault();

            if (point != null)
            {
                var node = vm.GetBarItemAtIndex(point.Index);
                if (node != null && node.IsDirectory)
                {
                    vm.SelectedNode = node;
                }
            }
        }
    }

    private void OnPieChartPointerMoved(object? sender, PointerEventArgs e)
    {
        try
        {
            var pos = e.GetPosition(PieChartControl);
            var lvcPoint = new LvcPointD(pos.X, pos.Y);
            var points = PieChartControl.GetPointsAt(lvcPoint);
            var point = points.FirstOrDefault();

            if (point != null && DataContext is MainWindowViewModel vm)
            {
                var name = point.Context?.Series?.Name ?? "Unknown";
                var size = (long)point.Coordinate.PrimaryValue;
                var formattedSize = FormatSize(size);

                string percentStr = "";
                string itemCountStr = "";
                if (vm.SelectedNode != null)
                {
                    var totalSize = vm.SelectedNode.Children.Sum(c => c.TotalSize);
                    if (totalSize > 0)
                    {
                        var percent = (double)size / totalSize * 100;
                        percentStr = $" ({percent:0.#}%)";
                    }

                    var node = vm.SelectedNode.Children.FirstOrDefault(c => c.Name == name);
                    if (node != null && node.IsDirectory && node.TotalItemCount > 0)
                    {
                        itemCountStr = $" • {node.TotalItemCount:N0} items";
                    }
                    else if (name == "Other" && vm.GetOtherVirtualNode() is { } otherNode)
                    {
                        itemCountStr = $" • {otherNode.Children.Count} items";
                    }
                }

                PieChartHudText.Text = $"{name}: {formattedSize}{percentStr}{itemCountStr}";
                PieChartHud.IsVisible = true;
            }
            else
            {
                PieChartHud.IsVisible = false;
            }
        }
        catch
        {
            PieChartHud.IsVisible = false;
        }
    }

    private void OnPieChartPointerExited(object? sender, PointerEventArgs e)
    {
        PieChartHud.IsVisible = false;
    }

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

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        CloseActiveContextMenu();
    }

    private void CloseActiveContextMenu()
    {
        if (_activeContextMenu != null)
        {
            _activeContextMenu.Close();
            _activeContextMenu = null;
        }
        _contextMenuTimer?.Stop();
    }

    private void ShowNodeContextMenu(FileSystemNode node, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var contextMenu = new ContextMenu();

        var openItem = new MenuItem { Header = "Open" };
        openItem.Click += (s, args) => vm.OpenCommand.Execute(node);

        var revealItem = new MenuItem { Header = "Reveal in File Manager" };
        revealItem.Click += (s, args) => vm.RevealCommand.Execute(node);

        var copyPathItem = new MenuItem { Header = "Copy Path" };
        copyPathItem.Click += (s, args) => vm.CopyPathCommand.Execute(node);

        var deleteItem = new MenuItem { Header = "Delete" };
        deleteItem.Click += (s, args) => vm.DeleteCommand.Execute(node);

        var exportItem = new MenuItem { Header = "Export to CSV" };
        exportItem.Click += (s, args) => vm.ExportNodeCommand.Execute(node);

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(revealItem);
        contextMenu.Items.Add(copyPathItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(deleteItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(exportItem);

        _activeContextMenu = contextMenu;

        contextMenu.Closed += (s, args) =>
        {
            if (_activeContextMenu == contextMenu)
                _activeContextMenu = null;
            _contextMenuTimer?.Stop();
        };

        _contextMenuTimer?.Stop();
        _contextMenuTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _contextMenuTimer.Tick += (s, args) =>
        {
            CloseActiveContextMenu();
        };
        _contextMenuTimer.Start();

        contextMenu.Open(this);
    }

    private async void SelectFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Folders/Drives to Scan (hold Ctrl to select multiple)",
                AllowMultiple = true
            });

            if (folders.Count > 0 && DataContext is MainWindowViewModel vm)
            {
                vm.ScanPaths.Clear();
                foreach (var folder in folders)
                {
                    var uri = folder.Path;
                    string path = uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
                    vm.AddScanPath(path);
                }

                Log.Information("Selected paths: {Paths}", string.Join(", ", vm.ScanPaths));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error selecting folder");
        }
    }

    private void RemoveScanPath_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && DataContext is MainWindowViewModel vm)
        {
            vm.RemoveScanPath(path);
        }
    }

    private void SearchResultsListBox_DoubleTapped(object? sender, global::Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (SearchResultsListBox.SelectedItem is FileSystemNode node)
        {
            vm.GoToNode(node);
        }
    }

    private void SearchResultGoToLocation_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (SearchResultsListBox.SelectedItem is FileSystemNode node)
        {
            vm.GoToNode(node);
        }
    }

    private void DuplicatesDataGrid_DoubleTapped(object? sender, global::Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (DuplicatesDataGrid.SelectedItem is DuplicateGroup group && group.Files.Count > 0)
        {
            var firstFile = group.Files[0];
            vm.GoToNode(firstFile);
        }
    }

    private void RevealDuplicateFile_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (DuplicatesDataGrid.SelectedItem is DuplicateGroup group && group.Files.Count > 0)
        {
            vm.RevealCommand.Execute(group.Files[0]);
        }
    }

    private async void ShowDuplicateLocations_Click(object? sender, RoutedEventArgs e)
    {
        if (DuplicatesDataGrid.SelectedItem is not DuplicateGroup group || group.Files.Count == 0)
            return;

        var dialog = new Window
        {
            Title = $"Duplicate Files ({group.Files.Count} locations)",
            Width = 600,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var listBox = new ListBox
        {
            ItemsSource = group.Files,
            Margin = new global::Avalonia.Thickness(10)
        };

        listBox.ItemTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<FileSystemNode>((node, _) =>
        {
            var panel = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Vertical, Margin = new global::Avalonia.Thickness(5) };
            panel.Children.Add(new TextBlock { Text = node.Name, FontWeight = global::Avalonia.Media.FontWeight.Bold });
            panel.Children.Add(new TextBlock { Text = node.FullPath, Foreground = global::Avalonia.Media.Brushes.Gray, FontSize = 11 });
            return panel;
        });

        listBox.DoubleTapped += (s, args) =>
        {
            if (listBox.SelectedItem is FileSystemNode selectedNode && DataContext is MainWindowViewModel vm)
            {
                vm.RevealCommand.Execute(selectedNode);
            }
        };

        dialog.Content = listBox;

        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            await dialog.ShowDialog(desktop.MainWindow);
        }
    }

    private MainWindowViewModel.DuplicateKeepStrategy GetSelectedStrategy()
    {
        return DuplicateKeepStrategy.SelectedIndex switch
        {
            0 => MainWindowViewModel.DuplicateKeepStrategy.KeepNewest,
            1 => MainWindowViewModel.DuplicateKeepStrategy.KeepOldest,
            2 => MainWindowViewModel.DuplicateKeepStrategy.KeepMostRecentAccess,
            3 => MainWindowViewModel.DuplicateKeepStrategy.KeepShortestPath,
            4 => MainWindowViewModel.DuplicateKeepStrategy.KeepLongestPath,
            _ => MainWindowViewModel.DuplicateKeepStrategy.KeepNewest
        };
    }

    private async void DeleteSelectedDuplicates_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var selectedGroups = DuplicatesDataGrid.SelectedItems.Cast<DuplicateGroup>().ToList();
        if (selectedGroups.Count == 0)
        {
            vm.StatusMessage = "No duplicate groups selected.";
            return;
        }

        var totalFiles = selectedGroups.Sum(g => g.Count - 1);
        var totalSize = selectedGroups.Sum(g => g.WastedSpace);

        var confirmed = await ShowDeleteConfirmation(
            $"Delete {totalFiles} duplicate files from {selectedGroups.Count} groups?",
            $"This will free approximately {FormatSize(totalSize)}.");

        if (confirmed)
        {
            await vm.DeleteDuplicatesAsync(selectedGroups, GetSelectedStrategy());
        }
    }

    private async void DeleteAllDuplicates_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (vm.DuplicateGroups.Count == 0)
        {
            vm.StatusMessage = "No duplicates found.";
            return;
        }

        var totalFiles = vm.DuplicateGroups.Sum(g => g.Count - 1);
        var totalSize = vm.DuplicateGroups.Sum(g => g.WastedSpace);

        var confirmed = await ShowDeleteConfirmation(
            $"Delete ALL {totalFiles} duplicate files from {vm.DuplicateGroups.Count} groups?",
            $"This will free approximately {FormatSize(totalSize)}.\n\nThis action cannot be undone!");

        if (confirmed)
        {
            await vm.DeleteDuplicatesAsync(vm.DuplicateGroups.ToList(), GetSelectedStrategy());
        }
    }

    private async void DeleteDuplicateGroup_KeepNewest_Click(object? sender, RoutedEventArgs e)
    {
        await DeleteDuplicateGroupWithStrategy(MainWindowViewModel.DuplicateKeepStrategy.KeepNewest);
    }

    private async void DeleteDuplicateGroup_KeepOldest_Click(object? sender, RoutedEventArgs e)
    {
        await DeleteDuplicateGroupWithStrategy(MainWindowViewModel.DuplicateKeepStrategy.KeepOldest);
    }

    private async void DeleteDuplicateGroup_KeepMostRecent_Click(object? sender, RoutedEventArgs e)
    {
        await DeleteDuplicateGroupWithStrategy(MainWindowViewModel.DuplicateKeepStrategy.KeepMostRecentAccess);
    }

    private async Task DeleteDuplicateGroupWithStrategy(MainWindowViewModel.DuplicateKeepStrategy strategy)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (DuplicatesDataGrid.SelectedItem is DuplicateGroup group)
        {
            var confirmed = await ShowDeleteConfirmation(
                $"Delete {group.Count - 1} duplicate files?",
                $"Keeping one file based on: {strategy.ToString().Replace("Keep", "")}");

            if (confirmed)
            {
                await vm.DeleteDuplicatesAsync(new[] { group }, strategy);
            }
        }
    }

    private async Task<bool> ShowDeleteConfirmation(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var result = false;

        var yesButton = new Button
        {
            Content = "Delete",
            Background = global::Avalonia.Media.Brushes.Red,
            Foreground = global::Avalonia.Media.Brushes.White,
            Margin = new global::Avalonia.Thickness(5),
            Padding = new global::Avalonia.Thickness(15, 5)
        };
        var noButton = new Button
        {
            Content = "Cancel",
            Margin = new global::Avalonia.Thickness(5),
            Padding = new global::Avalonia.Thickness(15, 5)
        };

        yesButton.Click += (s, e) => { result = true; dialog.Close(); };
        noButton.Click += (s, e) => { result = false; dialog.Close(); };

        var buttonPanel = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new global::Avalonia.Thickness(0, 15, 0, 0)
        };
        buttonPanel.Children.Add(yesButton);
        buttonPanel.Children.Add(noButton);

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Margin = new global::Avalonia.Thickness(20, 10)
        };

        var mainPanel = new StackPanel { Margin = new global::Avalonia.Thickness(10) };
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        dialog.Content = mainPanel;

        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            await dialog.ShowDialog(desktop.MainWindow);
        }

        return result;
    }
}
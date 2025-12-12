using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Seekr.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Seekr.Avalonia.Controls;

public class TreemapControl : Control
{
    private FileSystemNode? _root;
    private List<TreemapItem> _items = new();
    private TreemapItem? _hoveredItem;
    private Rect _headerBounds;
    
    // Define a nice palette
    private static readonly List<Color> Palette = new()
    {
        Color.Parse("#007ACC"), // Blue
        Color.Parse("#CA5100"), // Orange
        Color.Parse("#E81123"), // Red
        Color.Parse("#00CC6A"), // Green
        Color.Parse("#8E8CD8"), // Purple
        Color.Parse("#FFB900"), // Yellow
        Color.Parse("#00B7C3"), // Teal
        Color.Parse("#FF8C00"), // Dark Orange
    };

    public static readonly DirectProperty<TreemapControl, FileSystemNode?> RootProperty =
        AvaloniaProperty.RegisterDirect<TreemapControl, FileSystemNode?>(
            nameof(Root),
            o => o.Root,
            (o, v) => o.Root = v);

    public FileSystemNode? Root
    {
        get => _root;
        set
        {
            if (SetAndRaise(RootProperty, ref _root, value))
            {
                CalculateLayout();
                InvalidateVisual();
            }
        }
    }

    private class TreemapItem
    {
        public FileSystemNode Node { get; set; } = null!;
        public Rect Bounds { get; set; }
        public IBrush Brush { get; set; } = Brushes.Gray;
    }

    static TreemapControl()
    {
        AffectsRender<TreemapControl>(RootProperty);
        AffectsMeasure<TreemapControl>(RootProperty);
    }

    public TreemapControl()
    {
        // Enable pointer events
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        CalculateLayout();
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var item = _items.FirstOrDefault(i => i.Bounds.Contains(point));

        if (item != _hoveredItem)
        {
            _hoveredItem = item;
            InvalidateVisual();
            // Removed ToolTip.SetTip/SetIsOpen to fix glitching
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoveredItem = null;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        
        var point = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;

        // Check header click
        if (_headerBounds.Contains(point) && _root?.Parent != null)
        {
            Root = _root.Parent;
            return;
        }

        var item = _items.FirstOrDefault(i => i.Bounds.Contains(point));

        if (e.ClickCount == 2 && props.IsLeftButtonPressed)
        {
            // Drill down
            if (item != null && item.Node.IsDirectory)
            {
                Root = item.Node;
            }
        }
        else if (props.IsRightButtonPressed)
        {
            // Show Context Menu
            if (item != null)
            {
                ShowContextMenu(item.Node, point);
            }
        }
    }

    private void ShowContextMenu(FileSystemNode node, Point point)
    {
        var contextMenu = new ContextMenu();
        
        var openItem = new MenuItem { Header = "Open" };
        openItem.Click += (s, e) => 
        {
            // Invoke OpenCommand from ViewModel
            if (DataContext is Seekr.Avalonia.ViewModels.MainWindowViewModel vm)
                vm.OpenCommand.Execute(node);
        };
        
        var revealItem = new MenuItem { Header = "Reveal in Explorer" };
        revealItem.Click += (s, e) => 
        {
            if (DataContext is Seekr.Avalonia.ViewModels.MainWindowViewModel vm)
                vm.RevealCommand.Execute(node);
        };
        
        var copyPathItem = new MenuItem { Header = "Copy Path" };
        copyPathItem.Click += (s, e) => 
        {
            if (DataContext is Seekr.Avalonia.ViewModels.MainWindowViewModel vm)
                vm.CopyPathCommand.Execute(node);
        };

        var deleteItem = new MenuItem { Header = "Delete" };
        deleteItem.Click += (s, e) => 
        {
            if (DataContext is Seekr.Avalonia.ViewModels.MainWindowViewModel vm)
                vm.DeleteCommand.Execute(node);
        };

        var exportItem = new MenuItem { Header = "Export to CSV" };
        exportItem.Click += (s, e) => 
        {
            if (DataContext is Seekr.Avalonia.ViewModels.MainWindowViewModel vm)
                vm.ExportNodeCommand.Execute(node);
        };

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(revealItem);
        contextMenu.Items.Add(copyPathItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(deleteItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(exportItem);

        contextMenu.Open(this);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Draw background
        context.FillRectangle(Brushes.Transparent, Bounds);

        // Draw Header if applicable
        if (_root?.Parent != null)
        {
            context.FillRectangle(new SolidColorBrush(Color.Parse("#2D2D30")), _headerBounds);
            
            var headerText = new FormattedText(
                $"⬅ Go Up to {_root.Parent.Name}",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                14,
                Brushes.White
            );
            
            context.DrawText(headerText, new Point(_headerBounds.X + 10, _headerBounds.Y + (_headerBounds.Height - headerText.Height) / 2));
        }

        if (_items.Count == 0)
        {
            if (_root != null)
            {
                // Empty folder
                var text = new FormattedText(
                    "Empty Folder",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    14,
                    Brushes.Gray
                );
                context.DrawText(text, new Point(Bounds.Width / 2 - text.Width / 2, Bounds.Height / 2 - text.Height / 2));
            }
            else
            {
                // No selection
                var text = new FormattedText(
                    "Select a folder to view Treemap",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    14,
                    Brushes.Gray
                );
                context.DrawText(text, new Point(Bounds.Width / 2 - text.Width / 2, Bounds.Height / 2 - text.Height / 2));
            }
            return;
        }

        foreach (var item in _items)
        {
            var brush = item == _hoveredItem ? Brushes.Orange : item.Brush;
            
            // Fill
            context.FillRectangle(brush, item.Bounds);
            
            // Border
            context.DrawRectangle(new Pen(Brushes.White, 1), item.Bounds);

            // Text
            if (item.Bounds.Width > 40 && item.Bounds.Height > 20)
            {
                var fontSize = Math.Max(10, Math.Min(16, item.Bounds.Height / 3));
                
                var formattedText = new FormattedText(
                    item.Node.Name,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    fontSize,
                    Brushes.White
                );
                
                // Clip text to bounds
                if (formattedText.Width < item.Bounds.Width - 4 && formattedText.Height < item.Bounds.Height - 4)
                {
                    context.DrawText(formattedText, new Point(item.Bounds.X + 4, item.Bounds.Y + 4));
                    
                    // Draw size below name if enough space
                    if (item.Bounds.Height > formattedText.Height * 2 + 8)
                    {
                        var sizeText = new FormattedText(
                            FormatSize(item.Node.TotalSize),
                            System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            Typeface.Default,
                            fontSize * 0.8,
                            new SolidColorBrush(Color.Parse("#DDDDDD"))
                        );
                        
                        if (sizeText.Width < item.Bounds.Width - 4)
                        {
                            context.DrawText(sizeText, new Point(item.Bounds.X + 4, item.Bounds.Y + 4 + formattedText.Height));
                        }
                    }
                }
            }
        }

        // Draw Custom Tooltip (HUD)
        if (_hoveredItem != null)
        {
            var tooltipText = new FormattedText(
                $"{_hoveredItem.Node.Name}\n{FormatSize(_hoveredItem.Node.TotalSize)}",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                12,
                Brushes.White
            );

            var padding = 5;
            var tooltipWidth = tooltipText.Width + padding * 2;
            var tooltipHeight = tooltipText.Height + padding * 2;
            
            // Position near mouse but clamped to bounds
            // Since we don't have mouse pos in Render, we'll put it in a fixed location (bottom left)
            // or we can store mouse pos in OnPointerMoved.
            // Let's put it in the bottom-left corner as a status overlay.
            
            var tooltipRect = new Rect(10, Bounds.Height - tooltipHeight - 10, tooltipWidth, tooltipHeight);
            
            context.FillRectangle(new SolidColorBrush(Color.Parse("#AA000000")), tooltipRect);
            context.DrawRectangle(new Pen(Brushes.White, 1), tooltipRect);
            context.DrawText(tooltipText, new Point(tooltipRect.X + padding, tooltipRect.Y + padding));
        }
    }

    private void CalculateLayout()
    {
        _items.Clear();
        if (_root == null || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        double yOffset = 0;
        double availableHeight = Bounds.Height;

        // Reserve space for header if we are deep
        if (_root.Parent != null)
        {
            yOffset = 30;
            availableHeight -= yOffset;
            _headerBounds = new Rect(0, 0, Bounds.Width, yOffset);
        }
        else
        {
            _headerBounds = new Rect(0, 0, 0, 0);
        }

        if (_root.Children.Count == 0) return;

        var nodes = _root.Children.OrderByDescending(c => c.TotalSize).ToList();
        if (nodes.Count == 0) return;

        var totalSize = nodes.Sum(c => c.TotalSize);
        if (totalSize == 0) return;

        var bounds = new Rect(0, yOffset, Bounds.Width, availableHeight);
        ComputeRecursive(nodes, bounds, totalSize);
    }

    private void ComputeRecursive(List<FileSystemNode> nodes, Rect bounds, long totalSize)
    {
        if (nodes.Count == 0) return;

        if (nodes.Count == 1)
        {
            AddItem(nodes[0], bounds);
            return;
        }

        // Split along the longer axis to maintain aspect ratio
        bool splitVertical = bounds.Width > bounds.Height;
        
        long halfSize = totalSize / 2;
        long currentSize = 0;
        int splitIndex = 0;

        // Find the split point that gets closest to half size
        for (int i = 0; i < nodes.Count; i++)
        {
            if (currentSize + nodes[i].TotalSize > halfSize)
            {
                // Check if including this node is better or worse than excluding it
                long diffWith = Math.Abs((currentSize + nodes[i].TotalSize) - halfSize);
                long diffWithout = Math.Abs(currentSize - halfSize);
                
                if (diffWith < diffWithout)
                {
                    splitIndex = i;
                    currentSize += nodes[i].TotalSize;
                }
                else
                {
                    splitIndex = i - 1;
                }
                break;
            }
            currentSize += nodes[i].TotalSize;
            splitIndex = i;
        }
        
        if (splitIndex < 0) splitIndex = 0;
        if (splitIndex >= nodes.Count - 1) splitIndex = nodes.Count - 2; // Ensure at least one item in second group

        var group1 = nodes.Take(splitIndex + 1).ToList();
        var group2 = nodes.Skip(splitIndex + 1).ToList();

        long size1 = group1.Sum(x => x.TotalSize);
        long size2 = totalSize - size1;

        Rect rect1, rect2;

        if (splitVertical)
        {
            double w1 = bounds.Width * ((double)size1 / totalSize);
            rect1 = new Rect(bounds.X, bounds.Y, w1, bounds.Height);
            rect2 = new Rect(bounds.X + w1, bounds.Y, bounds.Width - w1, bounds.Height);
        }
        else
        {
            double h1 = bounds.Height * ((double)size1 / totalSize);
            rect1 = new Rect(bounds.X, bounds.Y, bounds.Width, h1);
            rect2 = new Rect(bounds.X, bounds.Y + h1, bounds.Width, bounds.Height - h1);
        }

        ComputeRecursive(group1, rect1, size1);
        ComputeRecursive(group2, rect2, size2);
    }

    private void AddItem(FileSystemNode node, Rect bounds)
    {
        // Add some padding
        var rect = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width), Math.Max(0, bounds.Height));
        if (rect.Width < 1 || rect.Height < 1) return;

        _items.Add(new TreemapItem
        {
            Node = node,
            Bounds = rect,
            Brush = GetBrushForNode(node)
        });
    }

    private IBrush GetBrushForNode(FileSystemNode node)
    {
        // Use a deterministic color based on name/extension
        int hash = node.Name.GetHashCode();
        int index = Math.Abs(hash) % Palette.Count;
        
        // Vary opacity or brightness based on depth or size? 
        // For now just use the palette
        return new SolidColorBrush(Palette[index]);
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
}

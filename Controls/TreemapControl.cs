using Seekr.Models;
using System.Drawing.Drawing2D;

namespace Seekr.Controls;

public partial class TreemapControl : UserControl
{
    private FileSystemNode? _root;
    private List<TreemapItem> _items = new();
    private TreemapItem? _hoveredItem;
    private readonly ToolTip _tooltip;
    private bool _isDark;

    public event Action<FileSystemNode>? NodeClicked;
    public event Action<FileSystemNode>? NodeHovered;
    public event Action<FileSystemNode, Point>? NodeRightClicked;

    private class TreemapItem
    {
        public FileSystemNode Node { get; set; } = null!;
        public RectangleF Bounds { get; set; }
        public Color Color { get; set; }
    }

    public TreemapControl()
    {
        InitializeComponent();
        
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        _tooltip = new ToolTip { AutoPopDelay = 5000, InitialDelay = 300, ReshowDelay = 100 };
        
        MouseMove += TreemapControl_MouseMove;
        MouseLeave += (s, e) => { _hoveredItem = null; Invalidate(); };
        MouseDoubleClick += TreemapControl_MouseDoubleClick;
        MouseClick += TreemapControl_MouseClick;
    }

    private void InitializeComponent()
    {
        Name = "TreemapControl";
        Size = new Size(400, 300);
        BackColor = Color.White;
    }

    public void ApplyTheme(bool isDark)
    {
        _isDark = isDark;
        BackColor = isDark ? Color.FromArgb(32, 32, 32) : Color.White;
        Invalidate();
    }

    public void SetData(FileSystemNode root)
    {
        _root = root;
        CalculateLayout();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        CalculateLayout();
        Invalidate();
    }

    private void CalculateLayout()
    {
        _items.Clear();
        if (_root == null || _root.Children.Count == 0 || Width <= 0 || Height <= 0) return;

        var nodes = _root.Children.OrderByDescending(c => c.TotalSize).ToList();
        if (nodes.Count == 0) return;

        var totalSize = nodes.Sum(c => c.TotalSize);
        if (totalSize == 0) return;

        // Simple slice-and-dice or squarified? 
        // Let's do a simple recursive slice-and-dice for MVP, or a row-based approach.
        // Actually, a simple row-based approach is easier to implement reliably in one go.
        
        // We'll use a simplified "Squarified" approach
        // 1. Normalize sizes to area
        var totalArea = (double)Width * Height;
        
        var bounds = new RectangleF(0, 0, Width, Height);
        ComputeSquarified(nodes, bounds, totalSize);
    }

    private void ComputeSquarified(List<FileSystemNode> nodes, RectangleF bounds, long totalSize)
    {
        // This is a simplified layout: Split bounds either horizontally or vertically based on aspect ratio
        // For a true squarified, we need a more complex algorithm.
        // Let's use a "Split Largest" approach which is decent.
        
        if (nodes.Count == 0) return;

        if (nodes.Count == 1)
        {
            AddItem(nodes[0], bounds);
            return;
        }

        // Split into two groups with roughly equal size
        long halfSize = totalSize / 2;
        long currentSize = 0;
        int splitIndex = 0;

        for (int i = 0; i < nodes.Count; i++)
        {
            currentSize += nodes[i].TotalSize;
            splitIndex = i;
            if (currentSize >= halfSize) break;
        }

        // If we picked all of them (because the last one was huge), back off one if possible
        if (splitIndex == nodes.Count - 1 && splitIndex > 0) splitIndex--;

        var group1 = nodes.Take(splitIndex + 1).ToList();
        var group2 = nodes.Skip(splitIndex + 1).ToList();

        long size1 = group1.Sum(x => x.TotalSize);
        long size2 = totalSize - size1;

        // Split bounds
        RectangleF rect1, rect2;
        
        if (bounds.Width > bounds.Height)
        {
            // Split vertically
            float w1 = (float)(bounds.Width * ((double)size1 / totalSize));
            rect1 = new RectangleF(bounds.X, bounds.Y, w1, bounds.Height);
            rect2 = new RectangleF(bounds.X + w1, bounds.Y, bounds.Width - w1, bounds.Height);
        }
        else
        {
            // Split horizontally
            float h1 = (float)(bounds.Height * ((double)size1 / totalSize));
            rect1 = new RectangleF(bounds.X, bounds.Y, bounds.Width, h1);
            rect2 = new RectangleF(bounds.X, bounds.Y + h1, bounds.Width, bounds.Height - h1);
        }

        ComputeSquarified(group1, rect1, size1);
        ComputeSquarified(group2, rect2, size2);
    }

    private void AddItem(FileSystemNode node, RectangleF bounds)
    {
        // Add padding
        var rect = new RectangleF(bounds.X + 1, bounds.Y + 1, Math.Max(0, bounds.Width - 2), Math.Max(0, bounds.Height - 2));
        if (rect.Width < 1 || rect.Height < 1) return;

        _items.Add(new TreemapItem
        {
            Node = node,
            Bounds = rect,
            Color = GetColorForNode(node)
        });
    }

    private Color GetColorForNode(FileSystemNode node)
    {
        // Generate color based on hash of name or extension
        var hash = node.Name.GetHashCode();
        var random = new Random(hash);
        
        // Generate a pleasing pastel-ish color
        // Hue: 0-360, Saturation: 0.5-0.8, Value: 0.6-0.9
        
        // Simple RGB generation for now
        int r = random.Next(50, 200);
        int g = random.Next(50, 200);
        int b = random.Next(50, 200);
        
        // Boost one channel to make it vibrant
        switch (random.Next(3))
        {
            case 0: r = Math.Min(255, r + 50); break;
            case 1: g = Math.Min(255, g + 50); break;
            case 2: b = Math.Min(255, b + 50); break;
        }

        return Color.FromArgb(r, g, b);
    }

    private void TreemapControl_MouseMove(object? sender, MouseEventArgs e)
    {
        var item = _items.FirstOrDefault(i => i.Bounds.Contains(e.Location));
        
        if (item != _hoveredItem)
        {
            _hoveredItem = item;
            Invalidate();
            
            if (_hoveredItem != null)
            {
                _tooltip.Show($"{_hoveredItem.Node.Name}\n{_hoveredItem.Node.FormattedSize}", this, e.Location.X + 10, e.Location.Y + 10);
                NodeHovered?.Invoke(_hoveredItem.Node);
            }
            else
            {
                _tooltip.Hide(this);
            }
        }
    }

    private void TreemapControl_MouseClick(object? sender, MouseEventArgs e)
    {
        if (_hoveredItem != null && e.Button == MouseButtons.Right)
        {
            NodeRightClicked?.Invoke(_hoveredItem.Node, PointToScreen(e.Location));
        }
    }

    private void TreemapControl_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (_hoveredItem != null && _hoveredItem.Node.IsDirectory)
        {
            NodeClicked?.Invoke(_hoveredItem.Node);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None; // Treemaps look better with crisp edges

        if (_root == null || _items.Count == 0)
        {
            using var font = new Font("Segoe UI", 10F);
            using var brush = new SolidBrush(_isDark ? Color.Gray : Color.Gray);
            var rect = ClientRectangle;
            var stringFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("Treemap (interactive) — scan results will appear here", font, brush, rect, stringFormat);
            return;
        }

        using var textBrush = new SolidBrush(_isDark ? Color.White : Color.Black);
        using var fontName = new Font("Segoe UI", 9F, FontStyle.Bold);
        using var fontSize = new Font("Segoe UI", 8F, FontStyle.Regular);

        foreach (var item in _items)
        {
            var rect = item.Bounds;
            
            // Fill
            var color = item.Color;
            if (item == _hoveredItem) color = ControlPaint.Light(color);
            
            using (var brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, rect);
            }

            // Draw text if enough space
            if (rect.Width > 40 && rect.Height > 20)
            {
                // Clip text to rectangle
                var state = g.Save();
                g.SetClip(rect);
                
                g.DrawString(item.Node.Name, fontName, textBrush, rect.X + 2, rect.Y + 2);
                
                if (rect.Height > 35)
                {
                    g.DrawString(item.Node.FormattedSize, fontSize, textBrush, rect.X + 2, rect.Y + 18);
                }
                
                g.Restore(state);
            }
        }
    }
}

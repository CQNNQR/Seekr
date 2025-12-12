using Seekr.Models;

namespace Seekr.Controls;

public partial class BarChartControl : UserControl
{
    private FileSystemNode? _root;
    private List<FileSystemNode> _sortedChildren = new();
    private readonly ToolTip _tooltip;
    private FileSystemNode? _hoveredNode;
    private VScrollBar _vScrollBar = null!;
    private bool _isDark = false;

    public event Action<FileSystemNode>? NodeClicked;
    public event Action<FileSystemNode>? NodeHovered;
    public event Action<FileSystemNode, Point>? NodeRightClicked;

    public BarChartControl()
    {
        InitializeComponent();
        
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        _tooltip = new ToolTip { AutoPopDelay = 5000, InitialDelay = 300, ReshowDelay = 100 };
        
        MouseMove += BarChartControl_MouseMove;
        MouseLeave += (s, e) => { _hoveredNode = null; Invalidate(); };
        MouseDoubleClick += BarChartControl_MouseDoubleClick;
        MouseClick += BarChartControl_MouseClick;
        MouseWheel += BarChartControl_MouseWheel;
    }

    public void ApplyTheme(bool isDark)
    {
        _isDark = isDark;
        BackColor = isDark ? Color.FromArgb(32, 32, 32) : Color.White;
        Invalidate();
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        
        _vScrollBar = new VScrollBar
        {
            Dock = DockStyle.Right,
            Visible = false
        };
        _vScrollBar.Scroll += (s, e) => Invalidate();
        Controls.Add(_vScrollBar);

        Name = "BarChartControl";
        Size = new Size(400, 300);
        BackColor = Color.White;
        BorderStyle = BorderStyle.None;

        ResumeLayout(false);
    }

    private void BarChartControl_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (_vScrollBar.Visible)
        {
            int newValue = _vScrollBar.Value - e.Delta;
            if (newValue < _vScrollBar.Minimum) newValue = _vScrollBar.Minimum;
            if (newValue > _vScrollBar.Maximum - _vScrollBar.LargeChange + 1) newValue = _vScrollBar.Maximum - _vScrollBar.LargeChange + 1;
            _vScrollBar.Value = newValue;
            Invalidate();
        }
    }

    public void SetData(FileSystemNode root)
    {
        _root = root;
        _sortedChildren = _root?.Children
            .OrderByDescending(c => c.TotalSize)
            .ToList() ?? new List<FileSystemNode>();
            
        UpdateScrollbar();
        Invalidate();
    }

    private void UpdateScrollbar()
    {
        if (_root == null || !_sortedChildren.Any())
        {
            _vScrollBar.Visible = false;
            return;
        }

        var childrenCount = _sortedChildren.Count;
        var padding = 10;
        var barHeight = 30;
        var gap = 5;
        var headerHeight = 30;
        
        var totalHeight = padding + headerHeight + (childrenCount * (barHeight + gap)) + padding;

        if (totalHeight > ClientSize.Height)
        {
            _vScrollBar.Visible = true;
            _vScrollBar.Maximum = totalHeight;
            _vScrollBar.LargeChange = ClientSize.Height;
            _vScrollBar.SmallChange = barHeight + gap;
        }
        else
        {
            _vScrollBar.Visible = false;
            _vScrollBar.Value = 0;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateScrollbar();
    }

    private void BarChartControl_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right && _hoveredNode != null)
        {
            NodeRightClicked?.Invoke(_hoveredNode, PointToScreen(e.Location));
        }
    }

    private void BarChartControl_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (_hoveredNode != null && _hoveredNode.IsDirectory && _hoveredNode.Children.Any())
        {
            // Just notify parent
            NodeClicked?.Invoke(_hoveredNode);
        }
    }

    private void BarChartControl_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_root == null || !_sortedChildren.Any()) return;

        var padding = 10;
        var barHeight = 30;
        var gap = 5;
        var headerHeight = 30;
        
        // Adjust Y for scroll
        var scrollOffset = _vScrollBar.Visible ? _vScrollBar.Value : 0;
        
        // Calculate index directly from Y coordinate
        var relativeY = e.Y - (padding + headerHeight - scrollOffset);
        var itemHeight = barHeight + gap;
        
        FileSystemNode? newHover = null;

        if (relativeY >= 0)
        {
            var index = relativeY / itemHeight;
            if (index >= 0 && index < _sortedChildren.Count)
            {
                // Check if within the bar height (ignoring gap)
                var offsetInItem = relativeY % itemHeight;
                if (offsetInItem < barHeight)
                {
                    newHover = _sortedChildren[index];
                }
            }
        }

        if (newHover != _hoveredNode)
        {
            _hoveredNode = newHover;
            Invalidate();
            if (_hoveredNode != null)
            {
                _tooltip.Show($"{_hoveredNode.Name}\n{_hoveredNode.FormattedSize}\n{(_hoveredNode.IsDirectory ? "Double-click to drill down" : "File")}", this, e.Location.X + 10, e.Location.Y + 10);
                NodeHovered?.Invoke(_hoveredNode);
            }
            else
            {
                _tooltip.Hide(this);
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_root == null)
        {
            DrawPlaceholder(g, "No data available");
            return;
        }

        if (!_sortedChildren.Any())
        {
            DrawPlaceholder(g, "Empty directory");
            return;
        }

        var padding = 10;
        var barHeight = 30;
        var gap = 5;
        var headerHeight = 30;
        
        var scrollOffset = _vScrollBar.Visible ? _vScrollBar.Value : 0;
        
        // Apply scroll transform? No, easier to just calculate Y positions so we can keep header fixed if we wanted (though here header scrolls too)
        // Actually, let's scroll the whole content including header
        
        var availableWidth = ClientSize.Width - padding * 2 - (_vScrollBar.Visible ? _vScrollBar.Width : 0);
        var maxVal = _sortedChildren.Max(c => c.TotalSize);
        if (maxVal == 0) maxVal = 1;

        // Draw header (scrolled)
        var headerY = padding - scrollOffset;
        if (headerY + 30 > 0) // Only draw if visible
        {
            using (var font = new Font("Segoe UI", 12F, FontStyle.Bold))
            using (var brush = new SolidBrush(_isDark ? Color.WhiteSmoke : Color.Black))
            {
                g.DrawString($"Contents of: {_root.Name}", font, brush, padding, headerY);
            }
        }

        var y = padding + headerHeight - scrollOffset;

        // Calculate start index to skip invisible items
        // y = padding + headerHeight - scrollOffset + index * (barHeight + gap)
        // We want y + barHeight >= 0
        // index * (barHeight + gap) >= scrollOffset - padding - headerHeight - barHeight
        var itemHeight = barHeight + gap;
        var startIndex = Math.Max(0, (scrollOffset - padding - headerHeight - barHeight) / itemHeight);

        for (int i = startIndex; i < _sortedChildren.Count; i++)
        {
            var child = _sortedChildren[i];
            
            // Recalculate y for this item
            y = padding + headerHeight - scrollOffset + i * itemHeight;

            // Optimization: Skip drawing if out of view
            if (y + barHeight < 0) continue; // Should be handled by startIndex but safe to keep
            if (y > ClientSize.Height) break;

            var pct = (double)child.TotalSize / maxVal;
            var barWidth = (int)(availableWidth * pct);
            if (barWidth < 1) barWidth = 1;
            
            var rect = new Rectangle(padding, y, barWidth, barHeight);
            var fullRect = new Rectangle(padding, y, availableWidth, barHeight);

            // Background for row
            if (child == _hoveredNode)
            {
                using var bgBrush = new SolidBrush(_isDark ? Color.FromArgb(60, 60, 60) : Color.FromArgb(240, 240, 255));
                g.FillRectangle(bgBrush, fullRect);
            }

            // Bar fill
            var color = GetColorForSize(child.TotalSize);
            if (child == _hoveredNode) color = ControlPaint.Light(color);
            
            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(rect, color, ControlPaint.Dark(color, 0.1f), 90f))
            {
                g.FillRectangle(brush, rect);
            }

            // Text
            var text = child.Name;
            var sizeText = child.FormattedSize;
            
            using (var font = new Font("Segoe UI", 9F, FontStyle.Regular))
            using (var boldFont = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (var textBrush = new SolidBrush(_isDark ? Color.WhiteSmoke : Color.Black))
            {
                // Name on left (inside bar if wide enough, else right)
                var nameSize = g.MeasureString(text, boldFont);
                if (barWidth > nameSize.Width + 10)
                {
                    g.DrawString(text, boldFont, Brushes.White, padding + 5, y + 6);
                }
                else
                {
                    g.DrawString(text, boldFont, textBrush, padding + barWidth + 5, y + 6);
                }

                // Size on far right - ensure it's visible even if bar is full width
                var sizeSize = g.MeasureString(sizeText, font);
                var sizeX = padding + availableWidth - sizeSize.Width;
                
                // If bar is very wide (near 100%), draw size text in white inside the bar
                if (barWidth > availableWidth - sizeSize.Width - 20)
                {
                    g.DrawString(sizeText, font, Brushes.White, sizeX, y + 6);
                }
                else
                {
                    g.DrawString(sizeText, font, _isDark ? Brushes.LightGray : Brushes.Gray, sizeX, y + 6);
                }
            }

            y += barHeight + gap;
        }
    }

    private void DrawPlaceholder(Graphics g, string text)
    {
        using var font = new Font("Segoe UI", 10F);
        using var brush = new SolidBrush(Color.Gray);
        var rect = ClientRectangle;
        var stringFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, rect, stringFormat);
    }

    private Color GetColorForSize(long size)
    {
        // Gradient from Green (small) to Red (large)
        // This is arbitrary, could be based on file type
        if (size > 1024L * 1024 * 1024) return Color.FromArgb(231, 76, 60); // > 1GB Red
        if (size > 100L * 1024 * 1024) return Color.FromArgb(243, 156, 18); // > 100MB Orange
        if (size > 10L * 1024 * 1024) return Color.FromArgb(52, 152, 219); // > 10MB Blue
        return Color.FromArgb(46, 204, 113); // Green
    }
}

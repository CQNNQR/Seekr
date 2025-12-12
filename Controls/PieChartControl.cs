using Seekr.Models;

namespace Seekr.Controls;

public partial class PieChartControl : UserControl
{
    private FileSystemNode? _rootNode;

    // Internal sector representation for pie chart
    private class Sector
    {
        public FileSystemNode Node { get; set; } = null!;
        public float StartAngle { get; set; }
        public float SweepAngle { get; set; }
        public Color Fill { get; set; }
        public bool Hovered { get; set; }
        public bool Selected { get; set; }
    }

    private List<Sector> _sectors = new();
    private Sector? _hovered = null;
    private Sector? _selected = null;
    private bool _isDark = false;

    private readonly ToolTip _tooltip;

    public event Action<FileSystemNode>? NodeClicked;
    public event Action<FileSystemNode>? NodeHovered;
    public event Action<FileSystemNode, Point>? NodeRightClicked;

    public PieChartControl()
    {
        InitializeComponent();

        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);

        _tooltip = new ToolTip { AutoPopDelay = 5000, InitialDelay = 300, ReshowDelay = 100 };

        MouseMove += PieChartControl_MouseMove;
        MouseLeave += (s, e) => { SetHovered(null); };
        MouseClick += PieChartControl_MouseClick;
        Resize += (s, e) => { if (_rootNode != null) { BuildLayout(); Invalidate(); } };
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

        Name = "PieChartControl";
        Size = new Size(400, 300);
        BackColor = Color.White;
        BorderStyle = BorderStyle.None;

        ResumeLayout(false);
    }

    public void SetData(FileSystemNode rootNode)
    {
        _rootNode = rootNode;
        BuildLayout();
        Invalidate();
    }

    private void BuildLayout()
    {
        _sectors.Clear();
        _hovered = null;
        _selected = null;

        if (_rootNode == null) return;

        var children = _rootNode.Children.OrderByDescending(c => c.TotalSize).ToList();
        if (!children.Any()) return;

        var total = Math.Max(1L, children.Sum(c => c.TotalSize));

        // Configurable thresholds
        const int MaxSlices = 30; // 100 is too cluttered, 30 is cleaner
        const float MinSweepAngle = 2.0f; // Minimum degrees to be its own slice

        var visibleChildren = new List<FileSystemNode>();
        var otherChildren = new List<FileSystemNode>();

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            float sweep = (float)((child.TotalSize / (double)total) * 360);

            // If we have room AND the slice is big enough, show it
            if (visibleChildren.Count < MaxSlices && sweep >= MinSweepAngle)
            {
                visibleChildren.Add(child);
            }
            else
            {
                otherChildren.Add(child);
            }
        }

        float currentAngle = 0;
        for (int i = 0; i < visibleChildren.Count; i++)
        {
            var child = visibleChildren[i];
            float sweep = (float)((child.TotalSize / (double)total) * 360);
            
            var sector = new Sector
            {
                Node = child,
                StartAngle = currentAngle,
                SweepAngle = sweep,
                Fill = GetFillForNode(child, i),
                Hovered = false,
                Selected = false
            };
            _sectors.Add(sector);
            currentAngle += sweep;
        }

        // Add "Other" sector if needed
        if (otherChildren.Any())
        {
            var otherSize = otherChildren.Sum(c => c.TotalSize);
            float sweep = (float)((otherSize / (double)total) * 360);
            
            // Create a functional node for "Other" that contains the smaller children
            var otherNode = new FileSystemNode
            {
                Name = "Other (smaller files)",
                Size = otherSize, // Base size
                IsDirectory = true, // Act as a container
                Parent = _rootNode
            };
            // Important: Add the children so we can drill down!
            otherNode.Children.AddRange(otherChildren);

            var sector = new Sector
            {
                Node = otherNode,
                StartAngle = currentAngle,
                SweepAngle = sweep,
                Fill = Color.Gray,
                Hovered = false,
                Selected = false
            };
            _sectors.Add(sector);
        }
    }

    private Color[] _palette = new[]
    {
        Color.FromArgb(68, 114, 196), Color.FromArgb(237, 125, 49), Color.FromArgb(165, 165, 165),
        Color.FromArgb(255, 192, 0), Color.FromArgb(91, 155, 213), Color.FromArgb(112, 173, 71),
        Color.FromArgb(38, 68, 120), Color.FromArgb(158, 72, 14), Color.FromArgb(99, 99, 99),
        Color.FromArgb(153, 115, 0), Color.FromArgb(37, 94, 145), Color.FromArgb(67, 104, 43)
    };

    private Color GetFillForNode(FileSystemNode node, int index)
    {
        return _palette[index % _palette.Length];
    }

    private void PieChartControl_MouseClick(object? sender, MouseEventArgs e)
    {
        if (_hovered != null)
        {
            if (e.Button == MouseButtons.Right)
            {
                NodeRightClicked?.Invoke(_hovered.Node, PointToScreen(e.Location));
                return;
            }

            _selected = _hovered;
            _selected.Selected = true;
            // Drill down into the selected sector
            if (_selected.Node.Children.Any())
            {
                // Just notify parent, don't update internal state yet
                NodeClicked?.Invoke(_selected.Node);
            }
            Invalidate();
        }
    }

    private void SetHovered(Sector? sector)
    {
        if (_hovered == sector) return;
        if (_hovered != null) _hovered.Hovered = false;
        _hovered = sector;
        if (_hovered != null) _hovered.Hovered = true;
        Invalidate();
        if (_hovered != null) NodeHovered?.Invoke(_hovered.Node);
    }

    private void PieChartControl_MouseMove(object? sender, MouseEventArgs e)
    {
        var sector = HitTest(e.Location);
        SetHovered(sector);
        if (sector != null)
        {
            _tooltip.Show($"{sector.Node.Name} — {sector.Node.FormattedSize}", this, e.Location.X + 12, e.Location.Y + 12);
        }
        else
        {
            _tooltip.Hide(this);
        }
    }

    private Sector? HitTest(Point p)
    {
        var center = new PointF(ClientSize.Width / 2f, ClientSize.Height / 2f);
        var outerRadius = Math.Min(ClientSize.Width, ClientSize.Height) / 2f - 40; // Match OnPaint padding
        var innerRadius = outerRadius * 0.5f; // 50% hole

        var dx = p.X - center.X;
        var dy = p.Y - center.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        
        if (dist > outerRadius || dist < innerRadius) return null;

        var angle = (float)(Math.Atan2(dy, dx) * 180 / Math.PI);
        if (angle < 0) angle += 360;

        foreach (var sector in _sectors)
        {
            if (angle >= sector.StartAngle && angle < sector.StartAngle + sector.SweepAngle)
            {
                return sector;
            }
        }
        return null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_rootNode == null || !_sectors.Any())
        {
            using var font = new Font("Segoe UI", 10F);
            using var brush = new SolidBrush(_isDark ? Color.Gray : Color.Gray);
            var rect = ClientRectangle;
            var stringFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("Pie chart (interactive) — scan results will appear here", font, brush, rect, stringFormat);
            return;
        }

        // Draw donut chart
        var center = new PointF(ClientSize.Width / 2f, ClientSize.Height / 2f);
        var outerRadius = Math.Min(ClientSize.Width, ClientSize.Height) / 2f - 40; // Increased padding to prevent clipping
        var innerRadius = outerRadius * 0.5f;

        foreach (var sector in _sectors)
        {
            DrawDonutSector(g, sector, center, innerRadius, outerRadius);
        }

        // Draw center info
        var infoNode = _hovered?.Node ?? _rootNode;
        if (infoNode != null)
        {
            var centerRect = new RectangleF(center.X - innerRadius, center.Y - innerRadius, innerRadius * 2, innerRadius * 2);
            // Inset slightly
            centerRect.Inflate(-10, -10);

            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            
            // Name
            using var nameFont = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            using var sizeFont = new Font("Segoe UI", 10F, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.Black);

            string name = infoNode.Name;
            if (name.Length > 20) name = name.Substring(0, 17) + "...";
            
            string size = infoNode.FormattedSize;
            string text = $"{name}\n{size}";
            
            g.DrawString(text, nameFont, textBrush, centerRect, sf);
        }
    }

    private void DrawDonutSector(Graphics g, Sector sector, PointF center, float innerRadius, float outerRadius)
    {
        // Hover/selected lift
        float lift = sector.Hovered ? 10f : (sector.Selected ? 5f : 0f);
        float currentOuter = outerRadius + lift;
        float currentInner = innerRadius; // Keep inner radius constant or animate if desired

        // Create path for donut slice
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        
        // Outer arc
        var outerRect = new RectangleF(center.X - currentOuter, center.Y - currentOuter, currentOuter * 2, currentOuter * 2);
        path.AddArc(outerRect, sector.StartAngle, sector.SweepAngle);
        
        // Inner arc (drawn backwards to create hole)
        var innerRect = new RectangleF(center.X - currentInner, center.Y - currentInner, currentInner * 2, currentInner * 2);
        path.AddArc(innerRect, sector.StartAngle + sector.SweepAngle, -sector.SweepAngle);
        
        path.CloseFigure();

        // Fill
        using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(outerRect, 
               ControlPaint.Light(sector.Fill, 0.2f), 
               ControlPaint.Dark(sector.Fill, 0.1f), 
               45f))
        {
            g.FillPath(brush, path);
        }

        // Border (gap)
        using var pen = new Pen(Color.White, 2f);
        g.DrawPath(pen, path);

        // Selection border
        if (sector.Selected || sector.Hovered)
        {
            using var selPen = new Pen(sector.Selected ? Color.Black : Color.FromArgb(100, Color.Black), 2f);
            g.DrawPath(selPen, path);
        }

        // Draw label only if sector is large enough
        if (sector.SweepAngle > 15)
        {
            var midAngle = sector.StartAngle + sector.SweepAngle / 2;
            var labelRadius = (currentInner + currentOuter) / 2;
            var labelX = center.X + (float)(labelRadius * Math.Cos(midAngle * Math.PI / 180));
            var labelY = center.Y + (float)(labelRadius * Math.Sin(midAngle * Math.PI / 180));

            // Simple percentage or size label
            string label = sector.Node.FormattedSize;
            if (sector.SweepAngle > 30) label = $"{sector.Node.Name}\n{label}";

            using var font = new Font("Segoe UI", 8F, FontStyle.Bold);
            using var brush = new SolidBrush(Color.White);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            
            // Drop shadow for text
            g.DrawString(label, font, Brushes.Black, labelX + 1, labelY + 1, sf);
            g.DrawString(label, font, brush, labelX, labelY, sf);
        }
    }
}

// Extension helper to draw rounded rectangle (simple)
static class GraphicsExt
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, RectangleF rect, int radius)
    {
        using var path = RoundedRectPath(rect, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, RectangleF rect, int radius)
    {
        using var path = RoundedRectPath(rect, radius);
        g.DrawPath(pen, path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRectPath(RectangleF r, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var d = radius * 2;
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

using Seekr.Models;
using Seekr.Services;
using Seekr.Controls;
using Serilog;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;

namespace Seekr.Forms;

public partial class MainForm : Form
{
    private DiskScanner? _scanner;
    private Models.ScanResult? _scanResult;
    private readonly AnalysisService _analysisService;

    public MainForm()
    {
        InitializeComponent();
        _analysisService = new AnalysisService();
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        
        // Form
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1200, 800);
        Text = "Seekr - Disk Usage Analyzer";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(800, 600);

        try
        {
            // Load icon from embedded resource for single-file compatibility
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Seekr.Seekr.ico");
            if (stream != null)
            {
                Icon = new Icon(stream);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load application icon");
        }

        // Create controls
        CreateControls();
        
        // Apply initial theme
        ThemeService.ApplyTheme(this, SettingsService.Settings.Theme);
        
        ResumeLayout(false);
        PerformLayout();
    }

    private Button btnSelectPath = null!;
    private Button btnStartScan = null!;
    private Button btnCancel = null!;
    // Keep a single, modeless diagnostics window instance so it does not block the main UI.
    private DiagnosticsForm? _diagnosticsForm;
    private TreeView treeViewResults = null!;
    private Panel pnlDetails = null!;
    private TextBox txtNodeDetails = null!;
    private ProgressBar progressBar = null!;
    private Label lblStatus = null!;
    private Label lblCurrentDir = null!;
    private Label lblSelectedPath = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel toolStripStatusLabel = null!;
    private SplitContainer splitContainer = null!;
    private Panel leftPanel = null!;
    private Panel rightPanel = null!;
    // View mode controls
    private Button btnModeTree = null!;
    private Button btnModePie = null!;
    private Button btnModeBar = null!;
    private Button btnModeTreemap = null!;
    private Button btnModeTopFiles = null!;
    private Button btnBack = null!;
    
    // Search controls
    private TextBox txtSearch = null!;
    private Button btnSearch = null!;
    private ListView listViewSearch = null!;
    private ListView listViewTopFiles = null!;
    private ToolTip _toolTip = null!;

    private PieChartControl? pieChartControl;
    private BarChartControl? barChartControl;
    private TreemapControl? treemapControl;

    private enum ViewMode { Tree, Pie, Bar, Treemap, TopFiles, Search }
    private ViewMode _currentMode = ViewMode.Tree;

    private void CreateControls()
    {
        // Initialize ToolTip
        _toolTip = new ToolTip
        {
            AutoPopDelay = 5000,
            InitialDelay = 1000,
            ReshowDelay = 500,
            ShowAlways = true
        };

        // Split container for layout
        splitContainer = new SplitContainer
        {
            // default orientation = vertical (left/right)
            Dock = DockStyle.Fill
        };

        // Defer setting the splitter distance and constraints until the form has a real client size
        this.Shown += (s, e) =>
        {
            try
            {
                // Now that the form is shown and splitContainer is docked/sized, we can set constraints
                splitContainer.Panel1MinSize = 300;
                splitContainer.Panel2MinSize = 300;

                var desiredSplitter = 350;
                var minSplitter = splitContainer.Panel1MinSize;
                var maxSplitter = Math.Max(0, this.ClientSize.Width - splitContainer.Panel2MinSize);
                var final = Math.Max(minSplitter, Math.Min(desiredSplitter, Math.Max(minSplitter, maxSplitter)));
                splitContainer.SplitterDistance = final;
            }
            catch
            {
                // ignore layout issues
            }
        };

        Controls.Add(splitContainer);

        // Top panel used for view mode buttons
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64, // Increased height to prevent button clipping
            Padding = new Padding(4), // Reduced padding
            BackColor = Color.FromArgb(240, 240, 240)
        };
        Controls.Add(topPanel);

        // Left panel for controls (kept on left side)
        leftPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BackColor = Color.WhiteSmoke
        };
        splitContainer.Panel1.Controls.Add(leftPanel);

        // Mode selector area (top-right in top panel)
        var modeFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(2), // Reduced padding
            Margin = new Padding(0),
            WrapContents = false
        };

        // Add explicit margins to buttons to center them vertically
        var btnMargin = new Padding(3, 10, 3, 3);
        
        btnModeTree = new Button { Text = "Tree View", Width = 110, Height = 32, Font = new Font("Segoe UI", 9F, FontStyle.Regular), Margin = btnMargin };
        btnModePie = new Button { Text = "Pie Chart", Width = 110, Height = 32, Font = new Font("Segoe UI", 9F, FontStyle.Regular), Margin = btnMargin };
        btnModeBar = new Button { Text = "Bar Chart", Width = 110, Height = 32, Font = new Font("Segoe UI", 9F, FontStyle.Regular), Margin = btnMargin };
        btnModeTreemap = new Button { Text = "Treemap", Width = 110, Height = 32, Font = new Font("Segoe UI", 9F, FontStyle.Regular), Margin = btnMargin };
        btnModeTopFiles = new Button { Text = "Top 100", Width = 110, Height = 32, Font = new Font("Segoe UI", 9F, FontStyle.Regular), Margin = btnMargin };
        btnBack = new Button { Text = "Back", Width = 80, Height = 32, Font = new Font("Segoe UI", 9F, FontStyle.Regular), Enabled = false, Margin = btnMargin };

        btnModeTree.Click += (s, e) => SetViewMode(ViewMode.Tree);
        btnModePie.Click += (s, e) => SetViewMode(ViewMode.Pie);
        btnModeBar.Click += (s, e) => SetViewMode(ViewMode.Bar);
        btnModeTreemap.Click += (s, e) => SetViewMode(ViewMode.Treemap);
        btnModeTopFiles.Click += (s, e) => SetViewMode(ViewMode.TopFiles);
        btnBack.Click += (s, e) => NavigateBack();

        // Settings button (Moved to top panel for visibility)
        var btnSettings = new Button 
        { 
            Text = "Settings", 
            Width = 80, 
            Height = 32, 
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Margin = btnMargin
        };
        _toolTip.SetToolTip(btnSettings, "Configure application settings");
        btnSettings.Click += (s, e) => 
        {
            // Pass the diagnostics action to the settings form
            Action openDiagnostics = () => BtnDiagnostics_Click(s, e);
            
            if (new SettingsForm(openDiagnostics).ShowDialog(this) == DialogResult.OK)
            {
                // Apply theme in case it changed
                ThemeService.ApplyTheme(this, SettingsService.Settings.Theme);

                // Refresh view to apply new settings (like units)
                if (_scanResult?.Root != null)
                {
                    // Force refresh of current view
                    SetViewMode(_currentMode);
                    // If tree view is active, we might need to refresh nodes text
                    if (_currentMode == ViewMode.Tree)
                    {
                        treeViewResults.BeginUpdate();
                        // Recursive refresh or just clear and repopulate?
                        // Repopulating is safer to ensure all formats update
                        PopulateTreeView(_scanResult.Root);
                        treeViewResults.EndUpdate();
                    }
                }
            }
        };

        modeFlow.Controls.Add(btnBack);
        modeFlow.Controls.Add(btnModeTree);
        modeFlow.Controls.Add(btnModePie);
        modeFlow.Controls.Add(btnModeBar);
        modeFlow.Controls.Add(btnModeTreemap);
        modeFlow.Controls.Add(btnModeTopFiles);
        modeFlow.Controls.Add(btnSettings);

        // add to the top panel so left panel layout remains untouched
        topPanel.Controls.Add(modeFlow);

        // Search Area (Left side of Top Panel)
        var searchFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Height = 40,
            Dock = DockStyle.Left,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6),
            Margin = new Padding(4),
            WrapContents = false
        };
        topPanel.Controls.Add(searchFlow);

        var lblSearch = new Label { Text = "Search:", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.DimGray, Padding = new Padding(0, 8, 4, 8) };
        txtSearch = new TextBox { Width = 200, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 8, 0, 0) };
        txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) PerformSearch(); };
        
        btnSearch = new Button { Text = "Find", Width = 60, Height = 27, Font = new Font("Segoe UI", 9F), Margin = new Padding(4, 6, 0, 0) };
        btnSearch.Click += (s, e) => PerformSearch();

        searchFlow.Controls.Add(lblSearch);
        searchFlow.Controls.Add(txtSearch);
        searchFlow.Controls.Add(btnSearch);

        // Right panel for tree view
        rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BackColor = Color.White
        };
        splitContainer.Panel2.Controls.Add(rightPanel);

        // --- Left Panel Content ---

        // Group: Path Selection
        var grpPath = new GroupBox
        {
            Text = "Target",
            Dock = DockStyle.Top,
            Height = 80, // Keep fixed height for path selection as it has absolute positioning
            Padding = new Padding(10),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        leftPanel.Controls.Add(grpPath);

        btnSelectPath = new Button
        {
            Text = "Select Path...",
            Location = new Point(10, 25),
            Size = new Size(100, 30),
            UseVisualStyleBackColor = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };
        btnSelectPath.Click += BtnSelectPath_Click;
        grpPath.Controls.Add(btnSelectPath);

        lblSelectedPath = new Label
        {
            Text = "No path selected",
            Location = new Point(120, 30),
            Size = new Size(200, 20),
            ForeColor = Color.DarkBlue,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };
        grpPath.Controls.Add(lblSelectedPath);

        // Group: Scan Operations
        var grpScan = new GroupBox
        {
            Text = "Operations",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        leftPanel.Controls.Add(grpScan);
        grpScan.BringToFront(); // Ensure correct stacking order (Dock.Top stacks from bottom up in code order usually, but let's be safe)

        var flowActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0),
            AutoSize = true
        };
        grpScan.Controls.Add(flowActions);

        btnStartScan = new Button
        {
            Text = "Start Scan",
            Width = 100,
            Height = 30,
            UseVisualStyleBackColor = true,
            Enabled = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Margin = new Padding(5)
        };
        btnStartScan.Click += BtnStartScan_Click;
        flowActions.Controls.Add(btnStartScan);

        btnCancel = new Button
        {
            Text = "Cancel",
            Width = 80,
            Height = 30,
            UseVisualStyleBackColor = true,
            Enabled = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Margin = new Padding(5)
        };
        btnCancel.Click += BtnCancel_Click;
        flowActions.Controls.Add(btnCancel);

        // Group: Progress
        var grpProgress = new GroupBox
        {
            Text = "Progress",
            Dock = DockStyle.Top,
            Height = 120,
            Padding = new Padding(10),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        leftPanel.Controls.Add(grpProgress);
        grpProgress.BringToFront();

        // Use TableLayoutPanel for robust layout
        var tlpProgress = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(0, 5, 0, 0)
        };
        tlpProgress.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F)); // Progress Bar
        tlpProgress.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F)); // Status
        tlpProgress.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Path
        tlpProgress.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        grpProgress.Controls.Add(tlpProgress);

        progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Style = ProgressBarStyle.Continuous,
            Margin = new Padding(0, 0, 0, 5)
        };
        tlpProgress.Controls.Add(progressBar, 0, 0);

        lblStatus = new Label
        {
            Text = "Ready to scan",
            Dock = DockStyle.Fill,
            ForeColor = Color.Green,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft
        };
        tlpProgress.Controls.Add(lblStatus, 0, 1);

        lblCurrentDir = new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            ForeColor = Color.DarkGray,
            Font = new Font("Consolas", 8F),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        tlpProgress.Controls.Add(lblCurrentDir, 0, 2);

        // Fix docking order (reverse of addition)
        grpProgress.SendToBack();
        grpScan.SendToBack();
        grpPath.SendToBack();

        // Results tree view
        var lblResults = new Label
        {
            Text = "Scan Results:",
            Dock = DockStyle.Top,
            Height = 30,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        rightPanel.Controls.Add(lblResults);

        // Details box container (Bottom)
        pnlDetails = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 140,
            Padding = new Padding(0, 10, 0, 0) // Top padding to separate from graphs
        };
        rightPanel.Controls.Add(pnlDetails);

        txtNodeDetails = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9F)
        };
        pnlDetails.Controls.Add(txtNodeDetails);

        treeViewResults = new TreeView
        {
            Dock = DockStyle.Fill,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            HideSelection = false,
            FullRowSelect = true
        };

        InitializeContextMenu();
        treeViewResults.ContextMenuStrip = _contextMenu;

        // Select node on right click
        treeViewResults.NodeMouseClick += (s, e) => {
            if (e.Button == MouseButtons.Right) 
            {
                treeViewResults.SelectedNode = e.Node;
                _contextMenuNode = e.Node.Tag as FileSystemNode;
            }
        };

        // Placeholder treemap and bar chart controls (stacked in same area)
        pieChartControl = new PieChartControl { Dock = DockStyle.Fill, Visible = false };
        barChartControl = new BarChartControl { Dock = DockStyle.Fill, Visible = false };
        treemapControl = new TreemapControl { Dock = DockStyle.Fill, Visible = false };

        // Search Results List
        listViewSearch = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Visible = false
        };
        listViewSearch.Columns.Add("Name", 300);
        listViewSearch.Columns.Add("Size", 100);
        listViewSearch.Columns.Add("Path", 400);
        
        listViewSearch.DoubleClick += (s, e) => {
            if (listViewSearch.SelectedItems.Count > 0)
            {
                var node = listViewSearch.SelectedItems[0].Tag as FileSystemNode;
                if (node != null)
                {
                    // Switch to tree view and select node
                    SetViewMode(ViewMode.Tree);
                    SelectNodeInTree(node);
                }
            }
        };

        // Top Files List
        listViewTopFiles = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Visible = false
        };
        listViewTopFiles.Columns.Add("Name", 300);
        listViewTopFiles.Columns.Add("Size", 100);
        listViewTopFiles.Columns.Add("Path", 400);
        
        listViewTopFiles.DoubleClick += (s, e) => {
            if (listViewTopFiles.SelectedItems.Count > 0)
            {
                var node = listViewTopFiles.SelectedItems[0].Tag as FileSystemNode;
                if (node != null)
                {
                    // Switch to tree view and select node
                    SetViewMode(ViewMode.Tree);
                    SelectNodeInTree(node);
                }
            }
        };
        
        // Context menu for Top Files
        listViewTopFiles.MouseClick += (s, e) => {
            if (e.Button == MouseButtons.Right && listViewTopFiles.SelectedItems.Count > 0)
            {
                var node = listViewTopFiles.SelectedItems[0].Tag as FileSystemNode;
                if (node != null) ShowContextMenu(node, listViewTopFiles.PointToScreen(e.Location));
            }
        };

        // No icons in the tree view — keep it text-only for clarity
        treeViewResults.ImageList = null;

        rightPanel.Controls.Add(treeViewResults);
        rightPanel.Controls.Add(pieChartControl);
        rightPanel.Controls.Add(barChartControl);
        rightPanel.Controls.Add(treemapControl);
        rightPanel.Controls.Add(listViewSearch);
        rightPanel.Controls.Add(listViewTopFiles);

        // Correct Z-Order for Docking:
        // Controls at the BOTTOM of Z-order (Back) are docked FIRST.
        // We want lblResults (Top) and pnlDetails (Bottom) to claim space first.
        // We want the charts (Fill) to take the remaining space.
        
        lblResults.SendToBack();
        pnlDetails.SendToBack();
        
        // Ensure charts are at the front (docked last, filling remaining space)
        treeViewResults.BringToFront();
        if (pieChartControl != null) pieChartControl.BringToFront();
        if (barChartControl != null) barChartControl.BringToFront();
        if (treemapControl != null) treemapControl.BringToFront();


        treeViewResults.AfterSelect += TreeViewResults_AfterSelect;
        treeViewResults.BeforeExpand += TreeViewResults_BeforeExpand;
        treeViewResults.NodeMouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                treeViewResults.SelectedNode = e.Node;
                if (e.Node.Tag is FileSystemNode fsNode)
                {
                    ShowContextMenu(fsNode, treeViewResults.PointToScreen(e.Location));
                }
            }
        };

        // Wire treemap interactions
        if (pieChartControl != null)
        {
            pieChartControl.NodeClicked += node =>
            {
                if (InvokeRequired) Invoke(() => { ShowNodeDetails(node); NavigateTo(node); }); 
                else { ShowNodeDetails(node); NavigateTo(node); }
            };

            pieChartControl.NodeHovered += node =>
            {
                if (InvokeRequired) Invoke(() => toolStripStatusLabel.Text = $"Hover: {node.Name} - {node.FormattedSize}"); else toolStripStatusLabel.Text = $"Hover: {node.Name} - {node.FormattedSize}";
            };

            pieChartControl.NodeRightClicked += (node, point) =>
            {
                if (InvokeRequired) Invoke(() => ShowContextMenu(node, point));
                else ShowContextMenu(node, point);
            };
        }

        // Wire bar chart interactions
        if (barChartControl != null)
        {
            barChartControl.NodeClicked += node =>
            {
                if (InvokeRequired) Invoke(() => { ShowNodeDetails(node); NavigateTo(node); }); 
                else { ShowNodeDetails(node); NavigateTo(node); }
            };

            barChartControl.NodeHovered += node =>
            {
                if (InvokeRequired) Invoke(() => toolStripStatusLabel.Text = $"Hover: {node.Name} - {node.FormattedSize}"); else toolStripStatusLabel.Text = $"Hover: {node.Name} - {node.FormattedSize}";
            };

            barChartControl.NodeRightClicked += (node, point) =>
            {
                if (InvokeRequired) Invoke(() => ShowContextMenu(node, point));
                else ShowContextMenu(node, point);
            };
        }

        // Wire treemap interactions
        if (treemapControl != null)
        {
            treemapControl.NodeClicked += node =>
            {
                if (InvokeRequired) Invoke(() => { ShowNodeDetails(node); NavigateTo(node); }); 
                else { ShowNodeDetails(node); NavigateTo(node); }
            };

            treemapControl.NodeHovered += node =>
            {
                if (InvokeRequired) Invoke(() => toolStripStatusLabel.Text = $"Hover: {node.Name} - {node.FormattedSize}"); else toolStripStatusLabel.Text = $"Hover: {node.Name} - {node.FormattedSize}";
            };

            treemapControl.NodeRightClicked += (node, point) =>
            {
                if (InvokeRequired) Invoke(() => ShowContextMenu(node, point));
                else ShowContextMenu(node, point);
            };
        }

        InitializeContextMenu();

        // Default view from settings
        if (Enum.TryParse<ViewMode>(SettingsService.Settings.DefaultViewMode, out var defaultMode))
        {
            SetViewMode(defaultMode);
        }
        else
        {
            SetViewMode(ViewMode.Tree);
        }

        // Status strip
        statusStrip = new StatusStrip();
        toolStripStatusLabel = new ToolStripStatusLabel("Ready");
        statusStrip.Items.Add(toolStripStatusLabel);
        Controls.Add(statusStrip);

        // Setup Tooltips
        _toolTip.SetToolTip(btnSelectPath, "Choose a folder or drive to scan");
        _toolTip.SetToolTip(btnStartScan, "Start the disk usage analysis");
        _toolTip.SetToolTip(btnCancel, "Stop the current scan");
        _toolTip.SetToolTip(btnBack, "Go back to the previous folder");
        
        _toolTip.SetToolTip(btnModeTree, "View as Tree (Hierarchical list)");
        _toolTip.SetToolTip(btnModePie, "View as Pie Chart (Distribution)");
        _toolTip.SetToolTip(btnModeBar, "View as Bar Chart (Comparison)");
        _toolTip.SetToolTip(btnModeTreemap, "View as Treemap (Space usage)");
        _toolTip.SetToolTip(btnModeTopFiles, "View Top 100 Largest Files");
        
        _toolTip.SetToolTip(txtSearch, "Search for files or folders by name");
        _toolTip.SetToolTip(btnSearch, "Execute search");
    }

    private void BtnSelectPath_Click(object? sender, EventArgs e)
    {
        Log.Verbose("User clicked 'Select Path' button");
        using var folderDialog = new FolderBrowserDialog
        {
            Description = "Select folder or drive to scan",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (folderDialog.ShowDialog() == DialogResult.OK)
        {
            lblSelectedPath.Text = folderDialog.SelectedPath;
            btnStartScan.Enabled = true;
            toolStripStatusLabel.Text = $"Selected: {folderDialog.SelectedPath}";
            Log.Verbose("User selected path: {SelectedPath}", folderDialog.SelectedPath);
        }
        else
        {
            Log.Verbose("User cancelled path selection dialog");
        }
    }

    private long _totalDriveBytes = 0;

    private async void BtnStartScan_Click(object? sender, EventArgs e)
    {
        Log.Verbose("User clicked 'Start Scan' button");
        var selectedPath = lblSelectedPath.Text;
        if (selectedPath == "No path selected" || !Directory.Exists(selectedPath))
        {
            MessageBox.Show("Please select a valid path to scan.", "Invalid Path", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Start scan
        btnStartScan.Enabled = false;
        btnCancel.Enabled = true;
        btnSelectPath.Enabled = false;
        
        // Determine if we can show deterministic progress
        _totalDriveBytes = 0;
        try
        {
            var root = Path.GetPathRoot(selectedPath);
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady)
                {
                    _totalDriveBytes = drive.TotalSize - drive.TotalFreeSpace;
                }
            }
        }
        catch { /* ignore drive info errors */ }

        if (_totalDriveBytes > 0)
        {
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 0;
            progressBar.Maximum = 100;
        }
        else
        {
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.MarqueeAnimationSpeed = 50;
        }
        
        lblStatus.Text = "Initializing scan...";
        lblStatus.ForeColor = Color.Blue;
        
        treeViewResults.Nodes.Clear();

        try
        {
            Log.Information("Starting scan for {SelectedPath}", selectedPath);
            // Use global settings
            var globalOptions = SettingsService.Settings.ScanOptions;
            var localOptions = new ScanOptions
            {
                ScanHiddenFiles = globalOptions.ScanHiddenFiles,
                ScanSystemFiles = globalOptions.ScanSystemFiles,
                MaxDepth = globalOptions.MaxDepth,
                FollowSymlinks = globalOptions.FollowSymlinks,
                ExcludedExtensions = globalOptions.ExcludedExtensions,
                ExcludedFolders = globalOptions.ExcludedFolders,
                RetryOnDriveNotReady = globalOptions.RetryOnDriveNotReady,
                RetryCount = globalOptions.RetryCount,
                RetryDelayMs = globalOptions.RetryDelayMs
            };

            // If the target root is hidden/system and the options would skip it, prompt the user
            try
            {
                var di = new DirectoryInfo(selectedPath);
                var asks = new List<string>();
                var enableHidden = false;
                var enableSystem = false;

                if ((di.Attributes & FileAttributes.Hidden) != 0 && !localOptions.ScanHiddenFiles)
                    asks.Add("hidden");

                if ((di.Attributes & FileAttributes.System) != 0 && !localOptions.ScanSystemFiles)
                    asks.Add("system");

                if (asks.Any())
                {
                    var what = string.Join(" and ", asks);
                    var prompt = $"The selected path appears to be marked as {what}.\nYour current scan settings would skip {what} files/directories.\n\nWould you like to include {what} for this scan?\n(Select Yes to include and proceed, No to cancel the scan.)";
                    var res = MessageBox.Show(prompt, "Scan settings - include hidden/system?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (res == DialogResult.Yes)
                    {
                        if (asks.Contains("hidden")) enableHidden = true;
                        if (asks.Contains("system")) enableSystem = true;
                    }
                    else
                    {
                        // user chose not to scan — abort starting the scan
                        btnStartScan.Enabled = true;
                        btnCancel.Enabled = false;
                        btnSelectPath.Enabled = true;
                        progressBar.Style = ProgressBarStyle.Blocks;
                        progressBar.MarqueeAnimationSpeed = 0;
                        lblStatus.Text = "Scan cancelled by user";
                        lblStatus.ForeColor = Color.Orange;
                        toolStripStatusLabel.Text = "Scan cancelled";
                        return;
                    }

                    if (enableHidden) localOptions.ScanHiddenFiles = true;
                    if (enableSystem) localOptions.ScanSystemFiles = true;
                }
            }
            catch (Exception ex)
            {
                // Non-fatal — proceed with defaults and let DiskScanner handle errors with logs
                Log.Warning(ex, "Failed to inspect selected path attributes before scanning: {Path}", selectedPath);
            }

            _scanner = new DiskScanner(localOptions);
            _scanner.ProgressChanged += Scanner_ProgressChanged;
            _scanner.CurrentDirectoryChanged += Scanner_CurrentDirectoryChanged;

            var progress = new Progress<int>(itemCount =>
            {
                // This is handled by Scanner_ProgressChanged now
            });

            // Run scanning in a retry loop so the user can re-try transient failures (e.g. drive not ready)
            while (true)
            {
                _scanResult = await _scanner.ScanAsync(selectedPath, progress);

                if (_scanResult.IsCancelled)
                {
                    Log.Information("Scan cancelled by user for {SelectedPath}", selectedPath);
                    lblStatus.Text = "Scan was cancelled";
                    lblStatus.ForeColor = Color.Orange;
                    toolStripStatusLabel.Text = "Scan cancelled";
                    break;
                }

                if (!string.IsNullOrEmpty(_scanResult.ErrorMessage))
                {
                    Log.Error("Scan failed for {SelectedPath}: {ErrorMessage}", selectedPath, _scanResult.ErrorMessage);
                    lblStatus.Text = "Scan failed";
                    lblStatus.ForeColor = Color.Red;
                    toolStripStatusLabel.Text = "Scan failed";

                    var prompt = $"Scan failed: {_scanResult.ErrorMessage}\n\nChoose 'Retry' to try again, 'Diagnostics' to inspect logs, or 'Cancel' to stop.";
                    var res = MessageBox.Show(prompt, "Scan Error", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);

                    // Yes = Retry, No = Diagnostics, Cancel = stop
                    if (res == DialogResult.Yes)
                    {
                        // retry loop
                        continue;
                    }

                    if (res == DialogResult.No)
                    {
                        ShowDiagnosticsWindow();
                        // after inspecting diagnostics, allow user to retry
                        continue;
                    }

                    // Cancel selected - break out of loop
                    break;
                }

                if (_scanResult.Root != null)
                {
                    PopulateTreeView(_scanResult.Root);
                    
                    // Reset navigation history for new scan
                    _navigationHistory.Clear();
                    NavigateTo(_scanResult.Root, false);
                    
                    // Switch to default view mode
                    if (Enum.TryParse<ViewMode>(SettingsService.Settings.DefaultViewMode, out var defaultMode))
                    {
                        SetViewMode(defaultMode);
                    }

                    // Force progress to 100% on completion
                    progressBar.Value = 100;

                    lblStatus.Text = $"Scan completed successfully! Found {_scanResult.Root.TotalItemCount:N0} items";
                    lblStatus.ForeColor = Color.Green;
                    toolStripStatusLabel.Text = $"Scan complete - {FileSystemNode.FormatBytes(_scanResult.Root.TotalSize)}";
                }
                else
                {
                    lblStatus.Text = "Scan returned no results";
                    lblStatus.ForeColor = Color.Orange;
                    toolStripStatusLabel.Text = "Scan returned no results";
                }

                // Completed (success or no-results) -> exit loop
                break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception during scan of {SelectedPath}", selectedPath);
            lblStatus.Text = $"Scan failed: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            toolStripStatusLabel.Text = "Scan failed";
            var res = MessageBox.Show($"An error occurred during scanning:\n{ex.Message}\n\nOpen Diagnostics?", "Scan Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            if (res == DialogResult.Yes)
            {
                ShowDiagnosticsWindow();
            }
        }
        finally
        {
            // Reset UI
            btnStartScan.Enabled = true;
            btnCancel.Enabled = false;
            btnSelectPath.Enabled = true;
            
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.MarqueeAnimationSpeed = 0;
            
            lblCurrentDir.Text = "";
            
            _scanner?.Dispose();
            _scanner = null;
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        Log.Verbose("User clicked 'Cancel' button");
        _scanner?.Cancel();
        Log.Information("User requested scan cancellation");
        lblStatus.Text = "Cancelling scan...";
        lblStatus.ForeColor = Color.Orange;
        btnCancel.Enabled = false;
    }

    private void BtnDiagnostics_Click(object? sender, EventArgs e)
    {
        Log.Verbose("User clicked 'Diagnostics' button");
        try
        {
            ShowDiagnosticsWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to open diagnostics: {ex.Message}", "Diagnostics Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowDiagnosticsWindow()
    {
        if (_diagnosticsForm == null || _diagnosticsForm.IsDisposed)
        {
            _diagnosticsForm = new DiagnosticsForm();
            // Remove reference when the form is closed
            _diagnosticsForm.FormClosed += (s, args) => _diagnosticsForm = null;
            _diagnosticsForm.Show(this); // modeless - does not block
        }
        else
        {
            if (!_diagnosticsForm.Visible)
                _diagnosticsForm.Show(this);
            _diagnosticsForm.BringToFront();
        }
    }

    private void Scanner_ProgressChanged(object? sender, ScanProgressEventArgs e)
    {
        // Use BeginInvoke to avoid blocking the scanner thread
        if (InvokeRequired)
        {
            BeginInvoke(() => Scanner_ProgressChanged(sender, e));
            return;
        }

        // Update status strip
        toolStripStatusLabel.Text = $"Items: {e.ItemsScanned:N0} | {e.Message}";
        
        // Update progress bar if we have a total size
        if (_totalDriveBytes > 0 && e.BytesScanned > 0)
        {
            var pct = (int)((double)e.BytesScanned / _totalDriveBytes * 100.0);
            // Clamp to 99% while running so user knows it's not done
            if (pct >= 100) pct = 99;
            if (pct < 0) pct = 0;
            progressBar.Value = pct;
            lblStatus.Text = $"Scanning... {pct}% ({FileSystemNode.FormatBytes(e.BytesScanned)})";
        }
        else
        {
            lblStatus.Text = $"Scanning... {e.ItemsScanned:N0} items";
        }
    }

    private void Scanner_CurrentDirectoryChanged(object? sender, string currentDir)
    {
        // Use BeginInvoke to avoid blocking the scanner thread
        if (InvokeRequired)
        {
            BeginInvoke(() => Scanner_CurrentDirectoryChanged(sender, currentDir));
            return;
        }

        lblCurrentDir.Text = TruncatePath(currentDir, 60);
    }

    private string TruncatePath(string path, int maxLength)
    {
        if (path.Length <= maxLength)
            return path;

        return "..." + path.Substring(path.Length - maxLength + 3);
    }

    private void PopulateTreeView(FileSystemNode rootNode)
    {
        treeViewResults.BeginUpdate();
        try
        {
            treeViewResults.Nodes.Clear();
            
            var rootTreeNode = new TreeNode(rootNode.ToString())
            {
                Tag = rootNode,
                ForeColor = GetColorForSize(rootNode.TotalSize),
                ImageKey = rootNode.IsDirectory ? "folder" : "file"
            };
            
            treeViewResults.Nodes.Add(rootTreeNode);
            PopulateNode(rootTreeNode, rootNode);
            
            rootTreeNode.Expand();
        }
        finally
        {
            treeViewResults.EndUpdate();
        }
    }

    private async void PerformSearch()
    {
        var query = txtSearch.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            SetViewMode(ViewMode.Tree);
            return;
        }

        if (_scanResult?.Root == null) return;

        SetViewMode(ViewMode.Search);
        listViewSearch.Items.Clear();
        
        // Simple loading indicator
        var loadingItem = new ListViewItem("Searching...");
        listViewSearch.Items.Add(loadingItem);
        
        try 
        {
            var matches = await Task.Run(() => SearchRecursive(_scanResult.Root, query));
            
            listViewSearch.BeginUpdate();
            listViewSearch.Items.Clear();
            
            if (matches.Count == 0)
            {
                listViewSearch.Items.Add(new ListViewItem("No results found."));
            }
            else
            {
                // Limit results to avoid UI freeze on massive matches
                var displayMatches = matches.Take(1000).ToList();
                
                foreach (var node in displayMatches)
                {
                    var item = new ListViewItem(node.Name);
                    item.SubItems.Add(node.FormattedSize);
                    item.SubItems.Add(node.FullPath);
                    item.Tag = node;
                    listViewSearch.Items.Add(item);
                }
                
                if (matches.Count > 1000)
                {
                    listViewSearch.Items.Add(new ListViewItem($"...and {matches.Count - 1000} more results. Refine your search."));
                }
            }
        }
        finally
        {
            listViewSearch.EndUpdate();
        }
    }

    private List<FileSystemNode> SearchRecursive(FileSystemNode node, string query)
    {
        var results = new List<FileSystemNode>();
        if (node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            results.Add(node);
        }

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                results.AddRange(SearchRecursive(child, query));
            }
        }
        return results;
    }

    private void SelectNodeInTree(FileSystemNode targetNode)
    {
        if (treeViewResults.Nodes.Count == 0) return;

        // Build path from root to target
        var path = new Stack<FileSystemNode>();
        var current = targetNode;
        while (current != null)
        {
            path.Push(current);
            current = current.Parent;
        }

        // Traverse TreeView
        if (path.Count == 0) return;
        
        var rootFsNode = path.Pop(); 
        var currentTreeNode = treeViewResults.Nodes[0];
        
        // Verify root matches (simple check)
        if (currentTreeNode.Tag != rootFsNode && currentTreeNode.Tag is FileSystemNode rootTag && rootTag != rootFsNode)
        {
            // If tags don't match reference, maybe check path?
            if (rootTag.FullPath != rootFsNode.FullPath) return;
        }

        while (path.Count > 0)
        {
            var nextFsNode = path.Pop();
            
            // Ensure expanded (this triggers lazy loading)
            currentTreeNode.Expand();
            
            // Find child
            TreeNode? nextTreeNode = null;
            foreach (TreeNode child in currentTreeNode.Nodes)
            {
                if (child.Tag == nextFsNode)
                {
                    nextTreeNode = child;
                    break;
                }
            }
            
            if (nextTreeNode != null)
            {
                currentTreeNode = nextTreeNode;
            }
            else
            {
                // Not found?
                break;
            }
        }
        
        treeViewResults.SelectedNode = currentTreeNode;
        currentTreeNode.EnsureVisible();
        treeViewResults.Focus();
    }

    private void SetViewMode(ViewMode mode)
    {
        try
        {
            _currentMode = mode;
            
            // Determine colors based on theme
            var isDark = SettingsService.Settings.Theme == "Dark";
            var activeColor = isDark ? Color.FromArgb(0, 120, 215) : Color.LightBlue;
            var activeText = isDark ? Color.White : Color.Black;
            var inactiveColor = isDark ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
            var inactiveText = isDark ? Color.WhiteSmoke : Color.Black;

            void UpdateButton(Button btn, bool isActive)
            {
                btn.BackColor = isActive ? activeColor : inactiveColor;
                btn.ForeColor = isActive ? activeText : inactiveText;
            }

            UpdateButton(btnModeTree, mode == ViewMode.Tree);
            UpdateButton(btnModePie, mode == ViewMode.Pie);
            UpdateButton(btnModeBar, mode == ViewMode.Bar);
            UpdateButton(btnModeTreemap, mode == ViewMode.Treemap);
            UpdateButton(btnModeTopFiles, mode == ViewMode.TopFiles);
            
            UpdateBackButton();

            // show/hide panels
        if (pieChartControl != null) pieChartControl.Visible = mode == ViewMode.Pie;
        if (barChartControl != null) barChartControl.Visible = mode == ViewMode.Bar;
        if (treemapControl != null) treemapControl.Visible = mode == ViewMode.Treemap;
        if (listViewSearch != null) listViewSearch.Visible = mode == ViewMode.Search;
        if (listViewTopFiles != null) listViewTopFiles.Visible = mode == ViewMode.TopFiles;
        treeViewResults.Visible = mode == ViewMode.Tree;
        
        // Hide details panel for Pie Chart to give it more space
        if (pnlDetails != null) pnlDetails.Visible = mode != ViewMode.Pie && mode != ViewMode.Treemap && mode != ViewMode.Search && mode != ViewMode.TopFiles;

        // If we already have a loaded result, push data into other views
        // No need to push data here anymore, as NavigateTo keeps them in sync.
        // Just ensure visibility is correct.
        
        if (mode == ViewMode.TopFiles && _currentContextNode != null)
        {
            ShowTopFiles(_currentContextNode);
        }

        Log.Verbose("View mode switched to {Mode}", mode.ToString());
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error switching view: " + ex.Message);
        }
    }

    private void UpdateBackButton()
    {
        btnBack.Enabled = _navigationHistory.Count > 0;
    }

    private void PopulateNode(TreeNode treeNode, FileSystemNode fsNode)
    {
        // Sort children by size (largest first) and then by name
        var sortedChildren = fsNode.Children
            .OrderByDescending(c => c.IsDirectory ? c.TotalSize : c.Size)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var child in sortedChildren)
        {
            var childTreeNode = new TreeNode(child.ToString())
            {
                Tag = child,
                ForeColor = GetColorForSize(child.TotalSize),
                ImageKey = child.IsDirectory ? "folder" : "file"
            };
            
            treeNode.Nodes.Add(childTreeNode);
            
            // Lazy loading: If directory has children, add a dummy node
            if (child.IsDirectory && child.Children.Any())
            {
                childTreeNode.Nodes.Add(new TreeNode("Dummy"));
            }
        }
    }

    private Color GetColorForSize(long size)
    {
        if (size < 10L * 1024 * 1024) return Color.Green; // < 10 MB
        if (size < 1024L * 1024 * 1024) return Color.Orange; // 10 MB - 1 GB
        return Color.Red; // > 1 GB
    }

    private void TreeViewResults_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        var node = e.Node;
        if (node == null) return;

        // Check if it has a dummy node
        if (node.Nodes.Count == 1 && node.Nodes[0].Text == "Dummy")
        {
            treeViewResults.BeginUpdate();
            try
            {
                node.Nodes.Clear();
                if (node.Tag is FileSystemNode fsNode)
                {
                    PopulateNode(node, fsNode);
                }
            }
            finally
            {
                treeViewResults.EndUpdate();
            }
        }
    }

    private void TreeViewResults_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is FileSystemNode node)
        {
            Log.Verbose("User selected TreeView node: {NodePath}", node.FullPath);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Path: {node.FullPath}");
            sb.AppendLine($"Type: {(node.IsDirectory ? "Directory" : "File")}");
            sb.AppendLine($"Size: {FileSystemNode.FormatBytes(node.TotalSize)}");
            sb.AppendLine($"Items: {node.TotalItemCount}");
            if (node.HasError)
            {
                sb.AppendLine();
                sb.AppendLine("Error:");
                sb.AppendLine(node.ErrorMessage ?? "(no message)");
            }

            txtNodeDetails.Text = sb.ToString();
        }
        else
        {
            txtNodeDetails.Text = string.Empty;
        }
    }

    private void ShowNodeDetails(FileSystemNode node)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Path: {node.FullPath}");
        sb.AppendLine($"Type: {(node.IsDirectory ? "Directory" : "File")}");
        sb.AppendLine($"Size: {FileSystemNode.FormatBytes(node.TotalSize)}");
        sb.AppendLine($"Items: {node.TotalItemCount}");
        if (node.HasError)
        {
            sb.AppendLine();
            sb.AppendLine("Error:");
            sb.AppendLine(node.ErrorMessage ?? "(no message)");
        }

        txtNodeDetails.Text = sb.ToString();
    }

    private void ShowTopFiles(FileSystemNode node)
    {
        if (listViewTopFiles == null) return;
        
        listViewTopFiles.BeginUpdate();
        listViewTopFiles.Items.Clear();
        
        try
        {
            var topFiles = _analysisService.GetTopFiles(node, 100);
            
            foreach (var file in topFiles)
            {
                var item = new ListViewItem(file.Name);
                item.SubItems.Add(file.FormattedSize);
                item.SubItems.Add(file.FullPath);
                item.Tag = file;
                listViewTopFiles.Items.Add(item);
            }
            
            if (topFiles.Count == 0)
            {
                listViewTopFiles.Items.Add(new ListViewItem("No files found in this directory."));
            }
        }
        finally
        {
            listViewTopFiles.EndUpdate();
        }
    }

    private Stack<FileSystemNode> _navigationHistory = new();
    private FileSystemNode? _currentContextNode;

    private void NavigateTo(FileSystemNode node, bool addToHistory = true)
    {
        if (_currentContextNode != null && addToHistory)
        {
            _navigationHistory.Push(_currentContextNode);
        }

        _currentContextNode = node;

        // Update all views
        if (pieChartControl != null) pieChartControl.SetData(node);
        if (barChartControl != null) barChartControl.SetData(node);
        if (treemapControl != null) treemapControl.SetData(node);
        
        if (_currentMode == ViewMode.TopFiles)
        {
            ShowTopFiles(node);
        }
        
        // Update TreeView selection if possible
        // Note: Finding the node in TreeView can be slow for large trees, so we might skip or optimize this
        
        UpdateBackButton();
    }

    private void NavigateBack()
    {
        if (_navigationHistory.Count > 0)
        {
            var prevNode = _navigationHistory.Pop();
            NavigateTo(prevNode, false);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Ensure diagnostics window is closed when main app closes
        try { _diagnosticsForm?.Close(); } catch { }

        _scanner?.Cancel();
        _scanner?.Dispose();
        base.OnFormClosing(e);
    }

    private ContextMenuStrip _contextMenu = null!;
    private FileSystemNode? _contextMenuNode;

    private void InitializeContextMenu()
    {
        _contextMenu = new ContextMenuStrip();
        
        var itemOpen = new ToolStripMenuItem("Open in Explorer");
        itemOpen.Click += (s, e) => OpenInExplorer(_contextMenuNode);
        
        var itemTerminal = new ToolStripMenuItem("Open in Terminal");
        itemTerminal.Click += (s, e) => OpenInTerminal(_contextMenuNode);
        
        var itemCopy = new ToolStripMenuItem("Copy Path");
        itemCopy.Click += (s, e) => CopyPath(_contextMenuNode);
        
        var itemProps = new ToolStripMenuItem("Properties");
        itemProps.Click += (s, e) => ShowProperties(_contextMenuNode);

        var itemDelete = new ToolStripMenuItem("Move to Recycle Bin");
        itemDelete.Click += (s, e) => DeleteToRecycleBin(_contextMenuNode);

        _contextMenu.Items.AddRange(new ToolStripItem[] { itemOpen, itemTerminal, itemCopy, new ToolStripSeparator(), itemDelete, itemProps });
    }

    private void ShowContextMenu(FileSystemNode node, Point location)
    {
        _contextMenuNode = node;
        _contextMenu.Show(location);
    }

    private void OpenInExplorer(FileSystemNode? node)
    {
        if (node == null) return;
        try
        {
            var path = node.FullPath;
            if (File.Exists(path))
                path = Path.GetDirectoryName(path);
                
            if (Directory.Exists(path))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex) { Log.Error(ex, "Failed to open explorer"); }
    }

    private void OpenInTerminal(FileSystemNode? node)
    {
        if (node == null) return;
        try
        {
            var path = node.FullPath;
            if (File.Exists(path))
                path = Path.GetDirectoryName(path);

            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo 
                { 
                    FileName = "cmd.exe",
                    Arguments = $"/k cd /d \"{path}\"",
                    UseShellExecute = true 
                });
            }
        }
        catch (Exception ex) { Log.Error(ex, "Failed to open terminal"); }
    }

    private void CopyPath(FileSystemNode? node)
    {
        if (node != null) Clipboard.SetText(node.FullPath);
    }

    private void ShowProperties(FileSystemNode? node)
    {
        if (node == null) return;
        try
        {
            ShowFileProperties(node.FullPath);
        }
        catch (Exception ex) { Log.Error(ex, "Failed to show properties"); }
    }

    private void DeleteToRecycleBin(FileSystemNode? node)
    {
        if (node == null) return;
        
        var result = MessageBox.Show($"Are you sure you want to move '{node.Name}' to the Recycle Bin?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result == DialogResult.Yes)
        {
            try
            {
                if (node.IsDirectory)
                    FileSystem.DeleteDirectory(node.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                else
                    FileSystem.DeleteFile(node.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    
                // Update model
                if (node.Parent != null)
                {
                    node.Parent.Children.Remove(node);
                    
                    // Propagate size change up the tree
                    var current = node.Parent;
                    while (current != null)
                    {
                        // Force calculation if not already done
                        var oldTotal = current.TotalSize; 
                        current.TotalSize = oldTotal - node.TotalSize;
                        
                        var oldItems = current.TotalItemCount;
                        current.TotalItemCount = oldItems - node.TotalItemCount;
                        
                        current = current.Parent;
                    }
                }
                else
                {
                    // Root node deleted?
                    _scanResult = null;
                    treeViewResults.Nodes.Clear();
                    NavigateTo(null, false);
                    return;
                }

                // Remove from tree view if it's the selected node
                if (treeViewResults.SelectedNode?.Tag == node)
                {
                    treeViewResults.SelectedNode.Remove();
                }
                
                // Refresh current view (charts, lists)
                if (_currentContextNode != null)
                {
                    // If we deleted the current context node, go up
                    if (_currentContextNode == node)
                    {
                        NavigateBack();
                    }
                    else
                    {
                        // Refresh current view to reflect changes
                        NavigateTo(_currentContextNode, false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpVerb;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpFile;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpParameters;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        public string lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    private const int SW_SHOW = 5;
    private const uint SEE_MASK_INVOKEIDLIST = 12;

    public static bool ShowFileProperties(string Filename)
    {
        SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
        info.cbSize = Marshal.SizeOf(info);
        info.lpVerb = "properties";
        info.lpFile = Filename;
        info.nShow = SW_SHOW;
        info.fMask = SEE_MASK_INVOKEIDLIST;
        return ShellExecuteEx(ref info);
    }
}

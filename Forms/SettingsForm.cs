using Seekr.Models;
using Seekr.Services;

namespace Seekr.Forms;

public class SettingsForm : Form
{
    private CheckBox chkScanHidden = null!;
    private CheckBox chkScanSystem = null!;
    private CheckBox chkFollowSymlinks = null!;
    private ComboBox cmbTheme = null!;
    private ComboBox cmbSizeUnit = null!;
    private ComboBox cmbDefaultView = null!;
    private Button btnSave = null!;
    private Button btnCancel = null!;
    private Button btnDiagnostics = null!;
    private readonly Action? _openDiagnosticsAction;

    public SettingsForm(Action? openDiagnosticsAction = null)
    {
        _openDiagnosticsAction = openDiagnosticsAction;
        InitializeComponent();
        try 
        { 
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Seekr.Seekr.ico");
            if (stream != null) Icon = new Icon(stream);
        } 
        catch { }
        LoadSettings();
        
        // Apply current theme initially
        ThemeService.ApplyTheme(this, SettingsService.Settings.Theme);
    }

    private void InitializeComponent()
    {
        Text = "Settings";
        Size = new Size(450, 500);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        // Main container
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            RowCount = 4,
            ColumnCount = 1
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Scan
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Appearance
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Troubleshooting
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Spacer/Buttons
        Controls.Add(mainLayout);

        // 1. Scan Options
        var grpScan = new GroupBox
        {
            Text = "Scan Options",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        mainLayout.Controls.Add(grpScan, 0, 0);

        var flowScan = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };
        grpScan.Controls.Add(flowScan);

        chkScanHidden = new CheckBox { Text = "Scan Hidden Files", AutoSize = true, Margin = new Padding(3, 3, 3, 5) };
        chkScanSystem = new CheckBox { Text = "Scan System Files", AutoSize = true, Margin = new Padding(3, 3, 3, 5) };
        chkFollowSymlinks = new CheckBox { Text = "Follow Symbolic Links", AutoSize = true, Margin = new Padding(3, 3, 3, 5) };

        flowScan.Controls.Add(chkScanHidden);
        flowScan.Controls.Add(chkScanSystem);
        flowScan.Controls.Add(chkFollowSymlinks);

        // 2. Appearance
        var grpAppearance = new GroupBox
        {
            Text = "Appearance",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 15, 0, 0)
        };
        mainLayout.Controls.Add(grpAppearance, 0, 1);

        var tableApp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 2,
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };
        tableApp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        tableApp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        grpAppearance.Controls.Add(tableApp);

        var lblTheme = new Label { Text = "Theme:", AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft };
        cmbTheme = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        cmbTheme.Items.AddRange(new object[] { "Light", "Dark" });
        // Preview theme on change
        cmbTheme.SelectedIndexChanged += (s, e) => 
        {
             if (cmbTheme.SelectedItem != null)
                ThemeService.ApplyTheme(this, cmbTheme.SelectedItem.ToString()!);
        };

        var lblUnit = new Label { Text = "Size Units:", AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft };
        cmbSizeUnit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        cmbSizeUnit.Items.AddRange(new object[] { "Auto", "Bytes", "KB", "MB", "GB" });

        var lblView = new Label { Text = "Default View:", AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft };
        cmbDefaultView = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        cmbDefaultView.Items.AddRange(new object[] { "Tree", "Pie", "Bar", "Treemap", "TopFiles" });

        tableApp.Controls.Add(lblTheme, 0, 0);
        tableApp.Controls.Add(cmbTheme, 1, 0);
        tableApp.Controls.Add(lblUnit, 0, 1);
        tableApp.Controls.Add(cmbSizeUnit, 1, 1);
        tableApp.Controls.Add(lblView, 0, 2);
        tableApp.Controls.Add(cmbDefaultView, 1, 2);

        // 3. Troubleshooting
        var grpTrouble = new GroupBox
        {
            Text = "Troubleshooting",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 15, 0, 0)
        };
        mainLayout.Controls.Add(grpTrouble, 0, 2);

        btnDiagnostics = new Button 
        { 
            Text = "Open Diagnostics Log", 
            AutoSize = true,
            Padding = new Padding(10, 5, 10, 5),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Enabled = _openDiagnosticsAction != null
        };
        btnDiagnostics.Click += (s, e) => _openDiagnosticsAction?.Invoke();
        
        var flowTrouble = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        flowTrouble.Controls.Add(btnDiagnostics);
        grpTrouble.Controls.Add(flowTrouble);

        // 4. Buttons (Bottom)
        var flowButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40,
            Margin = new Padding(0, 20, 0, 0)
        };
        
        btnSave = new Button { Text = "Save", DialogResult = DialogResult.OK, Width = 80, Height = 30 };
        btnSave.Click += (s, e) => SaveSettings();
        
        btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80, Height = 30 };
        
        flowButtons.Controls.Add(btnSave);
        flowButtons.Controls.Add(btnCancel);
        
        mainLayout.Controls.Add(flowButtons, 0, 3);
        flowButtons.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
    }

    private void LoadSettings()
    {
        var s = SettingsService.Settings;
        chkScanHidden.Checked = s.ScanOptions.ScanHiddenFiles;
        chkScanSystem.Checked = s.ScanOptions.ScanSystemFiles;
        chkFollowSymlinks.Checked = s.ScanOptions.FollowSymlinks;
        
        cmbTheme.SelectedItem = s.Theme;
        cmbSizeUnit.SelectedItem = s.SizeUnit;
        cmbDefaultView.SelectedItem = s.DefaultViewMode;
    }

    private void SaveSettings()
    {
        var s = SettingsService.Settings;
        s.ScanOptions.ScanHiddenFiles = chkScanHidden.Checked;
        s.ScanOptions.ScanSystemFiles = chkScanSystem.Checked;
        s.ScanOptions.FollowSymlinks = chkFollowSymlinks.Checked;
        
        s.Theme = cmbTheme.SelectedItem?.ToString() ?? "Light";
        s.SizeUnit = cmbSizeUnit.SelectedItem?.ToString() ?? "Auto";
        s.DefaultViewMode = cmbDefaultView.SelectedItem?.ToString() ?? "Tree";

        SettingsService.Save();
        Close();
    }
}

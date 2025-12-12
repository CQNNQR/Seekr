using System.Windows.Forms;
using Seekr.Services;
using Serilog;

namespace Seekr.Forms;

public class DiagnosticsForm : Form
{
    private TextBox txtLogs = null!;
    private Button btnRefresh = null!;
    private Button btnClear = null!;
    private Button btnCopy = null!;
    private Button btnClose = null!;
    private CheckBox chkAutoRefresh = null!;
    private System.Windows.Forms.Timer refreshTimer = null!;

    public DiagnosticsForm()
    {
        Text = "Diagnostics - Recent Logs";
        Width = 800;
        Height = 600;
        StartPosition = FormStartPosition.CenterParent;
        
        try 
        { 
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Seekr.Seekr.ico");
            if (stream != null) Icon = new Icon(stream);
        } 
        catch { }

        InitializeComponents();

        Load += (s, e) => RefreshLogs();
    }

    private void InitializeComponents()
    {
        txtLogs = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new System.Drawing.Font("Consolas", 9F),
            WordWrap = false
        };

        btnRefresh = new Button { Text = "Refresh", Width = 90, Height = 28, Left = 10, Top = 8 };
        btnClear = new Button { Text = "Clear View", Width = 90, Height = 28, Left = 110, Top = 8 };
        btnCopy = new Button { Text = "Copy", Width = 90, Height = 28, Left = 210, Top = 8 };
        btnClose = new Button { Text = "Close", Width = 90, Height = 28, Left = 310, Top = 8 };
        chkAutoRefresh = new CheckBox { Text = "Auto-refresh", Left = 420, Top = 12, AutoSize = true };
        chkAutoRefresh.Checked = true;
        chkAutoRefresh.Enabled = false;

        btnRefresh.Click += (s, e) => { Log.Verbose("User clicked 'Refresh' button in Diagnostics"); RefreshLogs(); };
        btnClear.Click += (s, e) => { Log.Verbose("User clicked 'Clear View' button in Diagnostics"); txtLogs.Clear(); };
        btnCopy.Click += (s, e) => { Log.Verbose("User clicked 'Copy' button in Diagnostics"); Clipboard.SetText(txtLogs.Text); };
        btnClose.Click += (s, e) => { Log.Verbose("User clicked 'Close' button in Diagnostics"); Close(); };

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 40 }; 
        topPanel.Controls.Add(btnRefresh);
        topPanel.Controls.Add(btnClear);
        topPanel.Controls.Add(btnCopy);
        topPanel.Controls.Add(btnClose);
        topPanel.Controls.Add(chkAutoRefresh);

        Controls.Add(txtLogs);
        Controls.Add(topPanel);

        refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        refreshTimer.Tick += (s, e) => { if (chkAutoRefresh.Checked) RefreshLogs(); };
        refreshTimer.Start();
    }

    private void RefreshLogs()
    {
        var lines = LogService.GetRecentLines(500);
        txtLogs.Lines = lines.ToArray();
        if (chkAutoRefresh.Checked && lines.Count > 0)
        {
            txtLogs.SelectionStart = txtLogs.TextLength;
            txtLogs.ScrollToCaret();
        }
    }
}

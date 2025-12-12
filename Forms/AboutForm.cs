using System.Reflection;

namespace Seekr.Forms;

public partial class AboutForm : Form
{
    public AboutForm()
    {
        InitializeComponent();
        try 
        { 
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Seekr.Seekr.ico");
            if (stream != null) Icon = new Icon(stream);
        } 
        catch { }
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        
        // Form
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(400, 350);
        Text = "About Seekr";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        // Create controls
        CreateControls();
        
        ResumeLayout(false);
        PerformLayout();
    }

    private void CreateControls()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        var copyright = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "Copyright © 2025";

        var lblTitle = new Label
        {
            Text = "Seekr",
            Location = new Point(20, 20),
            Size = new Size(360, 30),
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(lblTitle);

        var lblSubtitle = new Label
        {
            Text = "Lightning-Fast Disk Usage Analyzer",
            Location = new Point(20, 55),
            Size = new Size(360, 20),
            Font = new Font("Segoe UI", 10F),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.DarkBlue
        };
        Controls.Add(lblSubtitle);

        var lblVersion = new Label
        {
            Text = $"Version {version}",
            Location = new Point(20, 85),
            Size = new Size(360, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(lblVersion);

        var lblCopyright = new Label
        {
            Text = copyright,
            Location = new Point(20, 105),
            Size = new Size(360, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray
        };
        Controls.Add(lblCopyright);

        var linkLabel = new LinkLabel
        {
            Text = "www.cqnnqr.com",
            Location = new Point(20, 125),
            Size = new Size(360, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            LinkColor = Color.Blue
        };
        linkLabel.LinkClicked += (s, e) => 
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://www.cqnnqr.com", UseShellExecute = true }); } catch { }
        };
        Controls.Add(linkLabel);

        var lblDescription = new Label
        {
            Text = "A high-performance disk usage analyzer built with .NET 8 and WinForms.\n\n" +
                   "Features:\n" +
                   "• Fast multithreaded scanning\n" +
                   "• Responsive user interface\n" +
                   "• Detailed size breakdowns\n" +
                   "• Portable single executable\n\n" +
                   "Built for Windows with modern .NET technology.",
            Location = new Point(20, 155),
            Size = new Size(360, 140),
            TextAlign = ContentAlignment.TopLeft
        };
        Controls.Add(lblDescription);

        var btnOK = new Button
        {
            Text = "OK",
            Location = new Point(250, 300),
            Size = new Size(75, 25),
            DialogResult = DialogResult.OK,
            UseVisualStyleBackColor = true
        };
        btnOK.Click += (s, e) => Close();
        Controls.Add(btnOK);

        var btnNotices = new Button
        {
            Text = "Third Party Notices",
            Location = new Point(75, 300),
            Size = new Size(150, 25),
            UseVisualStyleBackColor = true
        };
        btnNotices.Click += (s, e) => 
        {
            try 
            { 
                // Extract embedded notices to temp file and open
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("Seekr.THIRD-PARTY-NOTICES.txt");
                if (stream != null)
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), "Seekr-THIRD-PARTY-NOTICES.txt");
                    using var fileStream = File.Create(tempPath);
                    stream.CopyTo(fileStream);
                    fileStream.Close(); // Ensure written
                    
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = tempPath, UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("Notices file not found in resources.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            } 
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open notices: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        Controls.Add(btnNotices);

        AcceptButton = btnOK;
    }
}

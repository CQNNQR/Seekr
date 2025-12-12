Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

# Clear
$g.Clear([System.Drawing.Color]::Transparent)

# Draw Circle Background (Dark Blue)
$rect = New-Object System.Drawing.Rectangle 16, 16, 224, 224
$brushBg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(30, 30, 40))
$g.FillEllipse($brushBg, $rect)

# Draw "Pie Chart" segments
$rectPie = New-Object System.Drawing.Rectangle 48, 48, 160, 160

# Segment 1 (Blue)
$brush1 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0, 120, 215))
$g.FillPie($brush1, $rectPie, 180, 200)

# Segment 2 (Orange)
$brush2 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 140, 0))
$g.FillPie($brush2, $rectPie, 20, 90)

# Segment 3 (Green)
$brush3 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0, 200, 80))
$g.FillPie($brush3, $rectPie, 110, 70)

# Draw "S" in the middle (White)
$font = New-Object System.Drawing.Font "Segoe UI", 100, [System.Drawing.FontStyle]::Bold
$brushText = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$format = New-Object System.Drawing.StringFormat
$format.Alignment = [System.Drawing.StringAlignment]::Center
$format.LineAlignment = [System.Drawing.StringAlignment]::Center

# Offset slightly to center visually
$rectText = New-Object System.Drawing.RectangleF 0, 10, 256, 256
$g.DrawString("S", $font, $brushText, $rectText, $format)

# Save as Icon
# We use a temporary file approach to convert Bitmap to Icon properly if possible, 
# but System.Drawing.Icon.FromHandle is the easiest way in pure PS without external tools.
$iconHandle = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($iconHandle)

$fileStream = New-Object System.IO.FileStream "Seekr.ico", "Create"
$icon.Save($fileStream)
$fileStream.Close()

$bmp.Dispose()
$g.Dispose()
Write-Host "Icon generated: Seekr.ico"

using System;
using System.Windows.Forms;

namespace Seekr;

/// <summary>
/// Service for applying themes to WinForms controls
/// </summary>
public static class ThemeService
{
    /// <summary>
    /// Applies the specified theme to a control and its children
    /// </summary>
    public static void ApplyTheme(Control control, string theme)
    {
        bool isDark = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);

        ApplyThemeToControl(control, isDark);
    }

    private static void ApplyThemeToControl(Control control, bool isDark)
    {
        // Apply theme to known chart controls
        if (control is Controls.PieChartControl pieChart)
        {
            pieChart.ApplyTheme(isDark);
        }
        else if (control is Controls.BarChartControl barChart)
        {
            barChart.ApplyTheme(isDark);
        }
        else if (control is Controls.TreemapControl treemap)
        {
            treemap.ApplyTheme(isDark);
        }

        // Recursively apply to child controls
        foreach (Control child in control.Controls)
        {
            ApplyThemeToControl(child, isDark);
        }
    }
}

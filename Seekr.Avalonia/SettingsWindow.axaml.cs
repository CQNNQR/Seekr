using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Seekr.Avalonia.ViewModels;

namespace Seekr.Avalonia;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
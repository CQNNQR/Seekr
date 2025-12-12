using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Seekr.Avalonia;

public partial class DetailsWindow : Window
{
    public DetailsWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

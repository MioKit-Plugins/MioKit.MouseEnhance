using Avalonia.Controls;
using MouseEnhance.Views;

namespace MouseEnhance.Preview.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LoadPluginPreview();
    }

    private void LoadPluginPreview()
    {
        // Layout preview without host DI: bind empty VM shell (toggles won't persist).
        PluginPreviewHost.Content = new MouseEnhanceConfigPage
        {
            DataContext = MouseEnhanceConfigViewModel.CreateDesignPreview()
        };
    }
}

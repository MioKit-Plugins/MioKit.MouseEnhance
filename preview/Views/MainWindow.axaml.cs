using Avalonia.Controls;
using Avalonia.Interactivity;
using MouseEnhance.Models;
using MouseEnhance.Services;

namespace MouseEnhance.Preview.Views;

public partial class MainWindow : Window
{
    private readonly FindMouseSpotlight _spotlight;

    public MainWindow()
    {
        InitializeComponent();
        _spotlight = new FindMouseSpotlight(ShowSpotlightError);
    }

    private void PreviewFindMouse(object? sender, RoutedEventArgs args)
    {
        SpotlightError.IsVisible = false;
        try
        {
            _spotlight.Show(new FindMouseSettings());
        }
        catch (Exception exception)
        {
            ShowSpotlightError(exception);
        }
    }

    private void ShowSpotlightError(Exception exception)
    {
        SpotlightError.Text = $"寻找鼠标预览失败。{exception.Message}";
        SpotlightError.IsVisible = true;
    }

    protected override void OnClosed(EventArgs args)
    {
        _spotlight.Hide();
        base.OnClosed(args);
    }

}

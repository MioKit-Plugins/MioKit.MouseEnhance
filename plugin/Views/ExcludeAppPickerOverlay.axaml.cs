using Avalonia;
using Avalonia.Controls;
using MouseEnhance.Models;
using MouseEnhance.Services;

namespace MouseEnhance.Views;

public partial class ExcludeAppPickerOverlay : Window
{
    public ExcludeAppPickerOverlay()
    {
        Win32Properties.AddWindowStylesCallback(this, FindMouseSpotlightNative.AddOverlayStyles);
        Win32Properties.AddWndProcHookCallback(this, FindMouseSpotlightNative.HandleOverlayHitTest);
        InitializeComponent();
    }

    public void MoveTo(int x, int y, int width, int height, string? title, string? processName)
    {
        Position = new PixelPoint(x, y);
        var scale = RenderScaling;
        Width = Math.Max(width / scale, 1);
        Height = Math.Max(height / scale, 1);

        var label = !string.IsNullOrWhiteSpace(processName)
            ? processName
            : string.IsNullOrWhiteSpace(title) ? "未命名窗口" : title;
        CaptionTitle.Text = label!;
        CaptionHost.IsVisible = Width >= 120 && Height >= 48;

        if (!IsVisible)
            Show();

        var handle = TryGetPlatformHandle()?.Handle ?? 0;
        if (handle != 0)
            GrabWindowNative.MoveResizeWindow(handle, new WindowRect(x, y, x + width, y + height));
    }
}

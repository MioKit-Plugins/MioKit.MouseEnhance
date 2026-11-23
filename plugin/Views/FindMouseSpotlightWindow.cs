using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using MouseEnhance.Services;

namespace MouseEnhance.Views;

internal sealed class FindMouseSpotlightWindow : Window
{
    private const double GlowRadiusMultiplier = 1.16;
    private readonly SpotlightSurface _surface;
    private PixelRect _screenBounds;

    public FindMouseSpotlightWindow(Color color, double maskOpacity, double glowOpacity)
    {
        Win32Properties.AddWindowStylesCallback(this, FindMouseSpotlightNative.AddOverlayStyles);
        Win32Properties.AddWndProcHookCallback(this, FindMouseSpotlightNative.HandleOverlayHitTest);
        Win32Properties.SetWindowCornerPreference(this, Win32Properties.WindowCornerPreference.DoNotRound);
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        CanMinimize = false;
        CanMaximize = false;
        WindowDecorations = WindowDecorations.None;
        Topmost = true;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        IsHitTestVisible = false;
        _surface = new SpotlightSurface(color, maskOpacity, glowOpacity);
        Content = _surface;
    }

    public void ShowOnDisplay(Screen display)
    {
        _screenBounds = display.Bounds;
        Position = _screenBounds.Position;
        Width = _screenBounds.Width / display.Scaling;
        Height = _screenBounds.Height / display.Scaling;

        if (ActualTransparencyLevel != WindowTransparencyLevel.Transparent)
            throw new InvalidOperationException("The spotlight requires a transparent window.");

        var handle = TryGetPlatformHandle()?.Handle ?? 0;
        if (handle == 0)
            throw new InvalidOperationException("The spotlight window has no native handle.");

        Show();
        // Force the native bounds after Avalonia's first layout, which rounds DIPs to pixels.
        FindMouseSpotlightNative.FitToScreen(handle, _screenBounds);
    }

    public void SetFrame(PixelPoint cursor, double physicalRadius, double opacity)
    {
        var outerRadius = physicalRadius * GlowRadiusMultiplier;
        if (cursor.X + outerRadius < _screenBounds.X || cursor.X - outerRadius > _screenBounds.Right ||
            cursor.Y + outerRadius < _screenBounds.Y || cursor.Y - outerRadius > _screenBounds.Bottom)
        {
            _surface.SetFrame(null, 0, opacity);
            return;
        }

        _surface.SetFrame(this.PointToClient(cursor), physicalRadius / RenderScaling, opacity);
    }

    private sealed class SpotlightSurface : Control
    {
        private readonly SolidColorBrush _mask = new(Colors.Black);
        private readonly RadialGradientBrush _glow;
        private readonly double _maskOpacity;
        private readonly double _glowOpacity;
        private Point? _center;
        private double _radius;
        private double _opacity;

        public SpotlightSurface(Color color, double maskOpacity, double glowOpacity)
        {
            _maskOpacity = maskOpacity;
            _glowOpacity = glowOpacity;
            _glow = new RadialGradientBrush
            {
                GradientStops =
                [
                    new GradientStop(Colors.Transparent, 1 / GlowRadiusMultiplier - 0.025),
                    new GradientStop(color, 1 / GlowRadiusMultiplier),
                    new GradientStop(Colors.Transparent, 1)
                ]
            };
            ClipToBounds = true;
        }

        public void SetFrame(Point? center, double radius, double opacity)
        {
            if (_center == center && _radius == radius && _opacity == opacity)
                return;

            _center = center;
            _radius = radius;
            _opacity = opacity;
            _mask.Opacity = opacity * _maskOpacity;
            _glow.Opacity = opacity * _glowOpacity;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            if (_opacity <= 0)
                return;

            var area = new Rect(Bounds.Size);
            if (_center is not { } center)
            {
                context.FillRectangle(_mask, area);
                return;
            }

            var hole = new EllipseGeometry(new Rect(center.X - _radius, center.Y - _radius, 2 * _radius, 2 * _radius));
            var mask = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(area), hole);
            context.DrawGeometry(_mask, null, mask);
            var glowRadius = _radius * GlowRadiusMultiplier;
            context.DrawEllipse(_glow, null, center, glowRadius, glowRadius);
        }
    }
}

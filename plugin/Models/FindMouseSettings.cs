namespace MouseEnhance.Models;

public sealed record FindMouseSettings
{
    private const string DefaultColor = "#8b5cf6";

    public bool Enabled { get; init; }
    public bool DoubleCtrlEnabled { get; init; } = true;
    public bool ShakeEnabled { get; init; }
    public string SpotlightColor { get; init; } = DefaultColor;
    public int SpotlightDiameter { get; init; } = 200;
    public int MaskOpacity { get; init; } = 74;
    public int GlowOpacity { get; init; } = 19;
    public int AnimationDurationMs { get; init; } = 400;
    public int HoldDurationMs { get; init; } = 800;
    public int ShakeThreshold { get; init; } = 7;

    internal FindMouseSettings Normalize() => this with
    {
        SpotlightColor = IsColor(SpotlightColor) ? SpotlightColor : DefaultColor,
        SpotlightDiameter = Math.Clamp(SpotlightDiameter, 48, 640),
        MaskOpacity = Math.Clamp(MaskOpacity, 0, 100),
        GlowOpacity = Math.Clamp(GlowOpacity, 0, 100),
        AnimationDurationMs = Math.Clamp(AnimationDurationMs, 0, 2000),
        HoldDurationMs = Math.Clamp(HoldDurationMs, 100, 5000),
        ShakeThreshold = Math.Clamp(ShakeThreshold, 3, 12)
    };

    private static bool IsColor(string? color) =>
        color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(char.IsAsciiHexDigit);
}

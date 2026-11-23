namespace MouseEnhance.Models;

public enum CursorWrapMode
{
    Both,
    Vertical,
    Horizontal
}

public enum CursorWrapActivation
{
    Always,
    Control,
    Shift
}

public sealed record CursorWrapSettings
{
    public bool Enabled { get; init; }
    public bool AutoActivate { get; init; }
    public bool DisableWhileDragging { get; init; } = true;
    public CursorWrapMode Mode { get; init; }
    public CursorWrapActivation Activation { get; init; }
    public bool DisableForSingleMonitor { get; init; }

    internal CursorWrapSettings Normalize() => this with
    {
        Mode = Enum.IsDefined(Mode) ? Mode : CursorWrapMode.Both,
        Activation = Enum.IsDefined(Activation) ? Activation : CursorWrapActivation.Always
    };
}

using MouseEnhance.Models;
using MouseEnhance.Services.Features;

internal static partial class Program
{
    private static readonly CursorWrapMonitor Primary = new(0, 0, 100, 100);

    private static void Main()
    {
        WrapsAtSingleMonitorEdges();
        RespectsDirectionAndMovement();
        LeavesSharedMonitorEdgesOpen();
        WrapsExposedEdgesOfStaggeredMonitors();
        WrapsAcrossGapsToTheFarthestMonitor();
        HandlesMissingAndDegenerateMonitors();
        RunControllerCases();
        Console.WriteLine("PASS: CursorWrap headless regression cases.");
    }

    private static void WrapsAtSingleMonitorEdges()
    {
        CursorWrapMonitor[] monitors = [Primary];

        AssertTarget(monitors, new(97, 40), new(99, 40), CursorWrapMode.Both,
            new(0, 40), "right wraps to left");
        AssertTarget(monitors, new(3, 40), new(0, 40), CursorWrapMode.Horizontal,
            new(99, 40), "left wraps to right");
        AssertTarget(monitors, new(40, 97), new(40, 99), CursorWrapMode.Both,
            new(40, 0), "bottom wraps to top");
        AssertTarget(monitors, new(40, 3), new(40, 0), CursorWrapMode.Vertical,
            new(40, 99), "top wraps to bottom");
    }

    private static void RespectsDirectionAndMovement()
    {
        CursorWrapMonitor[] monitors = [Primary];

        AssertNoTarget(monitors, new(97, 40), new(99, 40), CursorWrapMode.Vertical,
            "vertical mode leaves horizontal movement alone");
        AssertNoTarget(monitors, new(40, 97), new(40, 99), CursorWrapMode.Horizontal,
            "horizontal mode leaves vertical movement alone");
        AssertNoTarget(monitors, new(99, 39), new(99, 40), CursorWrapMode.Both,
            "moving along an edge does not wrap");
        AssertNoTarget(monitors, new(99, 40), new(99, 40), CursorWrapMode.Both,
            "a repeated boundary coordinate does not wrap");
        AssertNoTarget(monitors, new(99, 40), new(98, 40), CursorWrapMode.Both,
            "moving inward from a border does not wrap");

        AssertTarget(monitors, new(96, 94), new(99, 99), CursorWrapMode.Vertical,
            new(99, 0), "corner respects the enabled vertical axis");
        AssertTarget(monitors, new(96, 94), new(99, 99), CursorWrapMode.Horizontal,
            new(0, 99), "corner respects the enabled horizontal axis");
        AssertTarget(monitors, new(96, 94), new(99, 99), CursorWrapMode.Both,
            new(99, 0), "the dominant motion chooses the corner's wrap axis");
        AssertTarget(monitors, new(96, 96), new(99, 99), CursorWrapMode.Both,
            new(0, 99), "equal diagonal motion chooses horizontal wrapping");
    }

    private static void LeavesSharedMonitorEdgesOpen()
    {
        CursorWrapMonitor[] horizontal = [Primary, new(100, 0, 240, 100)];
        CursorWrapMonitor[] vertical = [Primary, new(0, 100, 100, 240)];

        AssertNoTarget(horizontal, new(98, 50), new(99, 50), CursorWrapMode.Both,
            "the right-hand neighbor keeps the shared edge open");
        AssertNoTarget(horizontal, new(98, 50), new(101, 50), CursorWrapMode.Both,
            "a natural move into a horizontal neighbor does not wrap");
        AssertNoTarget(vertical, new(50, 98), new(50, 99), CursorWrapMode.Both,
            "the lower neighbor keeps the shared edge open");
        AssertNoTarget(vertical, new(50, 98), new(50, 101), CursorWrapMode.Both,
            "a natural move into a vertical neighbor does not wrap");
        AssertTarget(horizontal, new(237, 50), new(239, 50), CursorWrapMode.Both,
            new(0, 50), "outer right wraps across a row of monitors");
        AssertTarget(vertical, new(50, 237), new(50, 239), CursorWrapMode.Both,
            new(50, 0), "outer bottom wraps across a column of monitors");
    }

    private static void WrapsExposedEdgesOfStaggeredMonitors()
    {
        CursorWrapMonitor[] monitors = [new(-160, -50, 0, 60), new(0, 0, 100, 120)];

        AssertNoTarget(monitors, new(-2, 0), new(-1, 0), CursorWrapMode.Both,
            "a staggered neighbor includes the first pixel of the shared edge");
        AssertNoTarget(monitors, new(-2, 59), new(-1, 59), CursorWrapMode.Both,
            "a staggered neighbor includes the last pixel of the shared edge");
        AssertTarget(monitors, new(-3, -1), new(-1, -1), CursorWrapMode.Both,
            new(-160, -1),
            "the exposed part of a negative-coordinate edge wraps within the actual monitor");
        AssertTarget(monitors, new(97, 70), new(99, 70), CursorWrapMode.Both,
            new(0, 70),
            "an absent row in the offset monitor cannot be the wrap destination");
    }

    private static void WrapsAcrossGapsToTheFarthestMonitor()
    {
        CursorWrapMonitor[] horizontal = [new(300, 0, 450, 100), Primary, new(150, 0, 250, 100)];
        CursorWrapMonitor[] vertical = [new(0, -300, 100, -200), Primary, new(0, -150, 100, -50)];

        AssertTarget(horizontal, new(2, 50), new(0, 50), CursorWrapMode.Both,
            new(449, 50), "outside left wraps beyond gaps to the rightmost matching monitor");
        AssertTarget(horizontal, new(447, 50), new(449, 50), CursorWrapMode.Both,
            new(0, 50), "outside right wraps beyond gaps to the leftmost matching monitor");
        AssertTarget(vertical, new(50, 97), new(50, 99), CursorWrapMode.Both,
            new(50, -300), "outside bottom wraps beyond gaps to the topmost matching monitor");
        AssertTarget(horizontal, new(97, 50), new(125, 50), CursorWrapMode.Both,
            new(0, 50), "an attempted move into a gap uses the previous display's exposed edge");
        AssertTarget([Primary], new(97, 97), new(120, 102), CursorWrapMode.Both,
            new(0, 99), "a diagonal overshoot clamps to the display's final row");
    }

    private static void HandlesMissingAndDegenerateMonitors()
    {
        AssertNoTarget([], new(97, 40), new(99, 40), CursorWrapMode.Both, "no monitor disables wrapping");
        AssertNoTarget([Primary], new(197, 40), new(199, 40), CursorWrapMode.Both,
            "coordinates outside every monitor do not wrap");
        AssertNoTarget([new(0, 0, 0, 100)], new(1, 40), new(0, 40), CursorWrapMode.Both,
            "a zero-width monitor is not a wrap target");
        AssertNoTarget([new(0, 0, 1, 100)], new(0, 40), new(1, 40), CursorWrapMode.Horizontal,
            "a one-pixel width cannot wrap to its own edge");
    }

    private static void AssertTarget(
        CursorWrapMonitor[] monitors,
        ScreenPoint previous,
        ScreenPoint current,
        CursorWrapMode mode,
        ScreenPoint expected,
        string scenario)
    {
        Assert(CursorWrapGeometry.TryGetTarget(monitors, previous, current, mode, out var target), scenario);
        Assert(target == expected, $"{scenario}, expected {expected}, got {target}");
        Assert(monitors.Any(monitor =>
            target.X >= monitor.Left && target.X < monitor.Right &&
            target.Y >= monitor.Top && target.Y < monitor.Bottom), $"{scenario}, target is on a monitor");
    }

    private static void AssertNoTarget(
        CursorWrapMonitor[] monitors,
        ScreenPoint previous,
        ScreenPoint current,
        CursorWrapMode mode,
        string scenario) =>
        Assert(!CursorWrapGeometry.TryGetTarget(monitors, previous, current, mode, out _), scenario);

    private static void Assert(bool condition, string scenario)
    {
        if (!condition)
            throw new InvalidOperationException(scenario);
    }
}

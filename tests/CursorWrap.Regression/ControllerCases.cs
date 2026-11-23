using System.Text.Json;
using MouseEnhance.Models;
using MouseEnhance.Services;
using MouseEnhance.Services.Features;

internal static partial class Program
{
    private static void RunControllerCases()
    {
        UsesSafeSettingsDefaults();
        RetainsLegacyMovementSettings();
        StartsAndStopsWithSettings();
        SetsActiveStateIdempotently();
        StartsAndStopsFromSearchCommands();
        KeepsRuntimeStateOnFailedSettingsChanges();
        RespectsActivationAndDragGuards();
        PreventsUnintendedMoves();
        HandlesFailedAndSuccessfulPositioning();
        ResetsOnlyTheFindMouseShake();
    }

    private static void UsesSafeSettingsDefaults()
    {
        var settings = JsonSerializer.Deserialize<CursorWrapSettings>("{}")!;
        Assert(!settings.Enabled && !settings.AutoActivate && settings.DisableWhileDragging &&
               !settings.DisableForSingleMonitor && settings.Mode == CursorWrapMode.Both &&
               settings.Activation == CursorWrapActivation.Always,
            "missing settings use the default opt-in behavior");

        var invalid = settings with { Mode = (CursorWrapMode)255, Activation = (CursorWrapActivation)255 };
        Assert(invalid.Normalize() == settings, "invalid saved enum values fall back safely");
    }

    private static void RetainsLegacyMovementSettings()
    {
        const string legacyJson = """
            { "Enabled": true, "AutoActivate": true, "DisableWhileDragging": false, "Mode": 2,
              "Activation": 2, "DisableForSingleMonitor": true, "Hotkey": 72057594043913746 }
            """;
        var expected = new CursorWrapSettings
        {
            Enabled = true,
            AutoActivate = true,
            DisableWhileDragging = false,
            Mode = CursorWrapMode.Horizontal,
            Activation = CursorWrapActivation.Shift,
            DisableForSingleMonitor = true
        };

        var settings = JsonSerializer.Deserialize<CursorWrapSettings>(legacyJson)!.Normalize();
        Assert(settings == expected, "legacy settings preserve movement preferences when loading a retired shortcut field");
        using var saved = JsonDocument.Parse(JsonSerializer.Serialize(settings));
        Assert(!saved.RootElement.TryGetProperty("Hotkey", out _), "settings saves omit the retired shortcut field");
    }

    private static void StartsAndStopsWithSettings()
    {
        var controller = new CursorWrapController();

        controller.Start(new CursorWrapSettings { Enabled = false, AutoActivate = true });
        Assert(!controller.IsActive, "a disabled feature stays inactive despite automatic activation");
        controller.Stop();

        var enabled = new CursorWrapSettings { Enabled = true, AutoActivate = true };
        controller.Start(enabled);
        Assert(controller.ReadState() == (enabled, true), "starting applies automatic activation and returns the configured state");
        controller.Stop();
        Assert(controller.ReadState() == (enabled, false), "stopping clears activation and retains the configured state");
    }

    private static void ResetsOnlyTheFindMouseShake()
    {
        var gestures = new FindMouseGestures();
        gestures.MouseMove(0, 30, 0, 3, enabled: true);
        gestures.MouseMove(100, 30, 10, 3, enabled: true);
        gestures.MouseMove(0, 30, 20, 3, enabled: true);
        gestures.MouseMove(100, 30, 30, 3, enabled: true);
        gestures.KeyDown(0xA2, 35, enabled: true);
        Assert(!gestures.KeyUp(0xA2, 45, enabled: true), "the first Ctrl tap only primes the sequence");

        gestures.ResetShake();
        Assert(!gestures.MouseMove(1, 30, 50, 3, enabled: true),
            "the first move after wrapping does not complete the old shake sequence");
        gestures.KeyDown(0xA2, 55, enabled: true);
        Assert(gestures.KeyUp(0xA2, 65, enabled: true), "rebasing the shake preserves the Ctrl sequence");
    }

    private static void SetsActiveStateIdempotently()
    {
        var controller = new CursorWrapController();
        var settings = new CursorWrapSettings { Enabled = true };
        controller.Start(settings);
        controller.UpdateMonitors([Primary]);
        var saved = new List<CursorWrapSettings>();

        controller.SetFeatureActive(true, saved.Add);
        Assert(controller.IsActive, "setting active starts an enabled controller");
        var pending = RequireRightEdgeMove(controller);
        controller.SetFeatureActive(true, saved.Add);
        Assert(controller.IsActive && controller.TryExecuteMove(pending, default, (_, _, _, _) => true),
            "repeated activation preserves the active state and its pending work");

        pending = RequireRightEdgeMove(controller);
        controller.SetFeatureActive(false, saved.Add);
        controller.SetFeatureActive(false, saved.Add);
        Assert(!controller.IsActive && controller.Settings == settings,
            "repeated deactivation pauses the session while retaining configured enablement");
        Assert(saved.Count == 0, "starting and stopping a configured feature does not rewrite settings");
        Assert(!controller.TryExecuteMove(pending, default, UnexpectedPositioning),
            "deactivation discards pending positioning");
        controller.Stop();
    }

    private static void StartsAndStopsFromSearchCommands()
    {
        var saved = new List<CursorWrapSettings>();
        var settings = new CursorWrapSettings { Mode = CursorWrapMode.Horizontal };
        var enabled = settings with { Enabled = true };
        var controller = new CursorWrapController();
        controller.Start(settings);

        controller.SetFeatureActive(false, saved.Add);
        Assert(!controller.IsActive && controller.Settings == settings && saved.Count == 0,
            "a stop command does not change an initially disabled feature");

        var persistenceError = new InvalidOperationException("test settings could not be persisted");
        AssertThrows(persistenceError, () => controller.SetFeatureActive(true, _ => throw persistenceError));
        Assert(!controller.IsActive && controller.Settings == settings,
            "a start command leaves an initially disabled feature intact if persistence fails");

        controller.SetFeatureActive(true, saved.Add);
        controller.SetFeatureActive(true, saved.Add);
        Assert(controller.IsActive && controller.Settings == enabled && saved.SequenceEqual([enabled]),
            "the first successful start enables and saves once while honoring existing preferences");

        controller.SetFeatureActive(false, saved.Add);
        controller.SetFeatureActive(false, saved.Add);
        Assert(!controller.IsActive && controller.Settings == enabled && saved.Count == 1,
            "search stop pauses wrapping while retaining enablement and saved preferences");

        controller.SetFeatureActive(true, saved.Add);
        Assert(controller.IsActive && saved.Count == 1, "starting a paused session resumes without another save");
        controller.Stop();
    }

    private static void KeepsRuntimeStateOnFailedSettingsChanges()
    {
        var original = new CursorWrapSettings { Enabled = true };
        var edited = original with { Mode = CursorWrapMode.Vertical, AutoActivate = true };
        var persistenceError = new InvalidOperationException("test settings could not be persisted");
        var controller = new CursorWrapController();
        controller.Start(original);

        controller.UpdateSettings(_ => edited, _ => { });
        Assert(controller.ReadState() == (edited, false),
            "successful settings replace the movement preferences while automatic activation applies on the next start");
        controller.Stop();
        controller.Start(edited);
        Assert(controller.IsActive, "restarting honors the new automatic activation preference");

        AssertThrows(persistenceError, () => controller.UpdateSettings(settings => settings with { Enabled = false },
            _ => throw persistenceError));
        Assert(controller.ReadState() == (edited, true),
            "failed persistence preserves the previous settings and runtime activation");

        controller.UpdateSettings(settings => settings with { Enabled = false }, _ => { });
        Assert(controller.ReadState() == (edited with { Enabled = false }, false),
            "disabling clears activation at runtime");
        controller.UpdateSettings(settings => settings with { Enabled = true, AutoActivate = false }, _ => { });
        Assert(controller.IsActive, "manually reenabling activates immediately regardless of the startup preference");
        controller.Stop();
    }

    private static void RespectsActivationAndDragGuards()
    {
        var controller = StartController(new CursorWrapSettings { Activation = CursorWrapActivation.Control });

        Assert(ObserveRightEdge(controller, new(ShiftHeld: true)) is null,
            "Shift alone does not satisfy Control activation");
        Assert(ObserveRightEdge(controller, new(ControlHeld: true, ButtonHeld: true)) is null,
            "held mouse buttons suppress wrapping by default");
        Assert(ObserveRightEdge(controller, new(ControlHeld: true, WindowDragActive: true)) is null,
            "window dragging takes priority over wrapping");
        Assert(ObserveRightEdge(controller, new(ControlHeld: true, IsSynthetic: true)) is null,
            "synthetic movement does not trigger wrapping");
        Assert(ObserveRightEdge(controller, new(ControlHeld: true)) is not null,
            "holding Control permits ordinary wrapping");

        var persisted = false;
        controller.UpdateSettings(settings => settings with
        {
            Activation = CursorWrapActivation.Shift,
            DisableWhileDragging = false,
            DisableForSingleMonitor = true
        }, _ => persisted = true);
        Assert(persisted, "applying settings invokes persistence");
        Assert(ObserveRightEdge(controller, new(ShiftHeld: true)) is null,
            "single-monitor suppression blocks even when the modifier is held");
        controller.UpdateMonitors([Primary, new(-100, 0, 0, 100)]);
        Assert(ObserveRightEdge(controller, new(ControlHeld: true)) is null,
            "after a live settings change, Control no longer satisfies Shift activation");
        Assert(ObserveRightEdge(controller, new(ShiftHeld: true, ButtonHeld: true)) is not null,
            "with two monitors, Shift activation permits dragging when configured");
    }

    private static void PreventsUnintendedMoves()
    {
        var controller = StartController(new CursorWrapSettings());
        var move = RequireRightEdgeMove(controller);
        Assert(!controller.TryExecuteMove(move, new(ButtonHeld: true), UnexpectedPositioning),
            "holding a button after observing the boundary prevents pending positioning");

        move = RequireRightEdgeMove(controller);
        Assert(controller.ObserveMove(new(98, 40), default) is null, "an inward move does not wrap");
        Assert(!controller.TryExecuteMove(move, default, UnexpectedPositioning),
            "a newer genuine move invalidates a queued older request");

        controller.ObserveMove(new(97, 40), default);
        var overshoot = controller.ObserveMove(new(103, 40), default);
        var nextOvershoot = controller.ObserveMove(new(105, 40), default);
        Assert(overshoot is not null && nextOvershoot is { Previous: { X: 99, Y: 40 }, Target: { X: 0, Y: 40 } },
            "successive samples beyond the display can still wrap from its last physical edge pixel");

        move = RequireRightEdgeMove(controller);
        controller.UpdateMonitors([Primary, new(100, 0, 200, 100)]);
        Assert(!controller.TryExecuteMove(move, default, UnexpectedPositioning),
            "a display change invalidates positioning computed for an earlier topology");

        controller.UpdateMonitors([Primary]);
        move = RequireRightEdgeMove(controller);
        controller.Stop();
        Assert(!controller.TryExecuteMove(move, default, UnexpectedPositioning),
            "stopping discards pending positioning");
    }

    private static void HandlesFailedAndSuccessfulPositioning()
    {
        var controller = StartController(new CursorWrapSettings());
        var move = RequireRightEdgeMove(controller);
        Assert(!controller.TryExecuteMove(move, default, (_, _, _, _) => false),
            "a platform positioning failure is reported as failure");
        Assert(!controller.TryExecuteMove(move, default, UnexpectedPositioning),
            "an unsuccessful positioning request is used only once");

        move = RequireRightEdgeMove(controller);
        var positioned = false;
        Assert(controller.TryExecuteMove(move, default, (previous, current, target, monitors) =>
        {
            Assert(previous == new ScreenPoint(97, 40) && current == new ScreenPoint(99, 40),
                "positioning receives the observed source trajectory");
            Assert(target == new ScreenPoint(0, 40) && monitors.SequenceEqual([Primary]),
                "positioning receives the calculated target and display topology");
            positioned = true;
            return true;
        }) && positioned, "a current positioning operation succeeds");
        Assert(controller.ObserveMove(new(0, 40), new(IsSynthetic: true)) is null,
            "the relocation feedback does not trigger an immediate reverse wrap");
        Assert(controller.ObserveMove(new(0, 40), default) is null,
            "a repeated arrival at the destination does not trigger a reverse wrap");
        Assert(controller.ObserveMove(new(3, 40), default) is null, "moving inward does not wrap");
        Assert(controller.ObserveMove(new(0, 40), default) is not null,
            "a later intentional return to the destination edge can wrap again");
    }

    private static CursorWrapController StartController(CursorWrapSettings settings)
    {
        var controller = new CursorWrapController();
        controller.Start(settings with { Enabled = true, AutoActivate = true });
        controller.UpdateMonitors([Primary]);
        Assert(controller.IsActive, "the controller is automatically activated before exercising movement");
        return controller;
    }

    private static CursorWrapMove RequireRightEdgeMove(CursorWrapController controller) =>
        ObserveRightEdge(controller, default) ?? throw new InvalidOperationException("expected an eligible right-edge move");

    private static CursorWrapMove? ObserveRightEdge(CursorWrapController controller, CursorWrapInput input)
    {
        Assert(controller.ObserveMove(new(97, 40), input) is null, "an interior point cannot wrap");
        return controller.ObserveMove(new(99, 40), input);
    }

    private static bool UnexpectedPositioning(
        ScreenPoint first,
        ScreenPoint second,
        ScreenPoint third,
        IReadOnlyList<CursorWrapMonitor> monitors) =>
        throw new InvalidOperationException("positioning should never have been called");

    private static void AssertThrows(Exception expected, Action action)
    {
        try
        {
            action();
        }
        catch (Exception error) when (ReferenceEquals(error, expected))
        {
            return;
        }
        throw new InvalidOperationException($"expected {expected.Message}");
    }
}

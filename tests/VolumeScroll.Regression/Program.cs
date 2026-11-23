using MouseEnhance.Models;
using MouseEnhance.Services.Features;

var pressedKeys = new HashSet<int>();
var sentKeys = new List<byte>();
var acceptInput = true;
var volume = new VolumeScrollFeature(pressedKeys.Contains, key =>
{
    if (acceptInput)
        sentKeys.Add(key);
    return acceptInput;
});

Assert(!volume.TryHandleWheel(MouseEnhanceModifier.Alt, 0, 120) && sentKeys.Count == 0,
    "wheel without a modifier is passed through");
Assert(!volume.TryHandleWheel(MouseEnhanceModifier.Win, 0xA4, 120) && sentKeys.Count == 0,
    "an absorbed Alt cannot activate a Win binding");

Assert(volume.TryHandleWheel(MouseEnhanceModifier.Alt, 0xA5, 240) && sentKeys.SequenceEqual(new byte[] { 0xAF, 0xAF }),
    "an absorbed right Alt activates volume even when native key state is clear");
sentKeys.Clear();
Assert(volume.TryHandleWheel(MouseEnhanceModifier.Win, 0x5C, -120) && sentKeys.SequenceEqual(new byte[] { 0xAE }),
    "an absorbed right Win activates volume even when native key state is clear");

sentKeys.Clear();
pressedKeys.Add(0x12);
Assert(volume.TryHandleWheel(MouseEnhanceModifier.Alt, 0, -60) && sentKeys.SequenceEqual(new byte[] { 0xAE }),
    "native key state activates volume when window dragging did not absorb the modifier");
sentKeys.Clear();
Assert(!volume.TryHandleWheel(MouseEnhanceModifier.Alt, 0, 0) && sentKeys.Count == 0,
    "a zero wheel delta is passed through");
acceptInput = false;
Assert(!volume.TryHandleWheel(MouseEnhanceModifier.Alt, 0, 120) && sentKeys.Count == 0,
    "the wheel is passed through when volume injection is rejected");

Console.WriteLine("PASS: VolumeScroll headless regression cases.");

static void Assert(bool condition, string scenario)
{
    if (!condition)
        throw new InvalidOperationException(scenario);
}

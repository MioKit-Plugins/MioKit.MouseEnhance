using System.Runtime.InteropServices;
using MouseEnhance.Models;

namespace MouseEnhance.Services.Features;

/// <summary>Adjusts system volume with modifier + mouse wheel via media keys.</summary>
internal sealed class VolumeScrollFeature(Func<int, bool> isKeyDown, Func<byte, bool> sendVolumeKey)
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const byte VkVolumeUp = 0xAF;
    private const byte VkVolumeDown = 0xAE;

    public VolumeScrollFeature() : this(IsKeyDown, SendVolumeKey)
    {
    }

    public bool TryHandleWheel(MouseEnhanceModifier modifier, byte absorbedModifier, int delta)
    {
        if (delta == 0 || !IsModifierHeld(modifier, absorbedModifier))
            return false;

        var vk = delta > 0 ? VkVolumeUp : VkVolumeDown;
        var steps = Math.Clamp(Math.Abs((long)delta) / 120, 1, 4);
        var sentAny = false;
        for (var i = 0; i < steps; i++)
        {
            if (!sendVolumeKey(vk))
                break;
            sentAny = true;
        }

        return sentAny;
    }

    private bool IsModifierHeld(MouseEnhanceModifier modifier, byte absorbedModifier) => modifier switch
    {
        MouseEnhanceModifier.Win => absorbedModifier is 0x5B or 0x5C || isKeyDown(0x5B) || isKeyDown(0x5C),
        _ => absorbedModifier is 0x12 or 0xA4 or 0xA5 || isKeyDown(0x12)
    };

    private static bool IsKeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private static bool SendVolumeKey(byte vk)
    {
        NativeInput[] inputs =
        [
            CreateKey(vk, keyUp: false),
            CreateKey(vk, keyUp: true)
        ];
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>()) == inputs.Length;
    }

    private static NativeInput CreateKey(byte vk, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Data = new NativeInputUnion
        {
            Keyboard = new NativeKeyboardInput
            {
                Vk = vk,
                Scan = 0,
                Flags = KeyEventExtendedKey | (keyUp ? KeyEventKeyUp : 0u),
                Time = 0,
                ExtraInfo = 0
            }
        }
    };

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, NativeInput[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public NativeInputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct NativeInputUnion
    {
        [FieldOffset(0)] public NativeMouseInput Mouse;
        [FieldOffset(0)] public NativeKeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeKeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }
}

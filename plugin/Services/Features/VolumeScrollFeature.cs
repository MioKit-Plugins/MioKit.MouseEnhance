using System.Runtime.InteropServices;
using MouseEnhance.Models;

namespace MouseEnhance.Services.Features;

/// <summary>Adjusts system volume with modifier + mouse wheel via media keys.</summary>
internal sealed class VolumeScrollFeature
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const byte VkVolumeUp = 0xAF;
    private const byte VkVolumeDown = 0xAE;

    public bool TryHandleWheel(MouseEnhanceModifier modifier, int delta)
    {
        if (!IsModifierHeld(modifier) || delta == 0)
            return false;

        var vk = delta > 0 ? VkVolumeUp : VkVolumeDown;
        var steps = Math.Clamp(Math.Abs(delta) / 120, 1, 4);
        for (var i = 0; i < steps; i++)
            SendVolumeKey(vk);

        return true;
    }

    private static bool IsModifierHeld(MouseEnhanceModifier modifier)
    {
        return modifier switch
        {
            MouseEnhanceModifier.Win =>
                (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0,
            _ =>
                (GetAsyncKeyState(0x12) & 0x8000) != 0 // VK_MENU
        };
    }

    private static void SendVolumeKey(byte vk)
    {
        NativeInput[] inputs =
        [
            CreateKey(vk, keyUp: false),
            CreateKey(vk, keyUp: true)
        ];
        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>());
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

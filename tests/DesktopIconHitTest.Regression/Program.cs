using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using MouseEnhance.Models;
using MouseEnhance.Services.Features;

// Integration regression: host a real native SysListView32 in a separate process.
// Pointer-bearing LVM_* setup messages below are ONLY sent inside that fixture.
// The test process exercises production Oleacc/COM interop across process boundaries.
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--fixture"))
        {
            RunFixture();
            return 0;
        }

        using var fixture = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--fixture")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        }) ?? throw new InvalidOperationException("Could not start fixture.");

        try
        {
            var json = fixture.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15))
                .GetAwaiter().GetResult();
            var data = JsonSerializer.Deserialize<FixtureData>(json!)!;
            var listView = (nint)data.ListView;
            // Production runs on the thread pool (MTA), not the UI/STA thread.
            Task.Run(() => CheckHits(data)).GetAwaiter().GetResult();

            Native.PostMessage((nint)data.Window, 0x0010, 0, 0); // WM_CLOSE, owned fixture only
            Assert(fixture.WaitForExit(5000), "fixture closes normally");
            Assert(fixture.ExitCode == 0, "fixture did not crash");
            Assert(!DesktopIconHitTest.IsEmpty(listView, data.Empty), "destroyed window fails closed");
            Console.WriteLine("PASS: cross-process icon, label, empty, outside, missing/destroyed window and 100 repeated pairs on MTA.");
            return 0;
        }
        finally
        {
            if (!fixture.HasExited)
                fixture.Kill();
        }
    }

    private static void CheckHits(FixtureData data)
    {
        var listView = (nint)data.ListView;
        Assert(!DesktopIconHitTest.IsEmpty(0, data.Empty), "missing window fails closed");
        Assert(!DesktopIconHitTest.IsEmpty(listView, data.Icon), "icon is not empty space");
        Assert(!DesktopIconHitTest.IsEmpty(listView, data.Label), "label is not empty space");
        Assert(DesktopIconHitTest.IsEmpty(listView, data.Empty), "empty client space is detected");
        Assert(!DesktopIconHitTest.IsEmpty(listView, data.Outside), "outside result fails closed");

        for (var i = 0; i < 100; i++)
        {
            Assert(DesktopIconHitTest.IsEmpty(listView, data.Empty), $"repeated empty hit {i}");
            Assert(!DesktopIconHitTest.IsEmpty(listView, data.Label), $"repeated label hit {i}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RunFixture()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        using var form = new Form
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-10000, -10000),
            ClientSize = new Size(640, 480),
            FormBorderStyle = FormBorderStyle.None
        };
        using var images = new ImageList { ImageSize = new Size(32, 32) };
        using var image = new Bitmap(32, 32);
        images.Images.Add(image);
        form.Shown += (_, _) =>
        {
            // Force common-controls initialization before creating the native control.
            using var initializer = new ListView();
            _ = initializer.Handle;
            var listView = Native.CreateWindowEx(0, "SysListView32", "Regression fixture",
                0x50000000, 0, 0, 640, 480, form.Handle, 0, 0, 0); // WS_CHILD | WS_VISIBLE | LVS_ICON
            Assert(listView != 0, "native list view created");
            Native.SendMessage(listView, 0x1003, 0, images.Handle); // LVM_SETIMAGELIST
            var item = new Native.ListViewItem
            {
                Mask = 3, // LVIF_TEXT | LVIF_IMAGE
                Text = "Regression icon",
                Image = 0
            };
            Assert(Native.InsertItem(listView, 0x104D, 0, ref item) == 0, "fixture item inserted");
            ScreenPoint AtRect(int part)
            {
                var rect = new Native.Rect { Left = part };
                Assert(Native.GetItemRect(listView, 0x100E, 0, ref rect) != 0, "fixture item bounds");
                var pt = new Native.Point { X = (rect.Left + rect.Right) / 2, Y = (rect.Top + rect.Bottom) / 2 };
                Native.ClientToScreen(listView, ref pt);
                return new ScreenPoint(pt.X, pt.Y);
            }

            var origin = new Native.Point();
            Native.ClientToScreen(listView, ref origin);
            var data = new FixtureData((long)form.Handle, (long)listView, AtRect(1), AtRect(2),
                new ScreenPoint(origin.X + 400, origin.Y + 300),
                new ScreenPoint(origin.X - 10, origin.Y - 10));
            Console.WriteLine(JsonSerializer.Serialize(data));
            Console.Out.Flush();
        };
        Application.Run(form);
    }

    private sealed record FixtureData(long Window, long ListView, ScreenPoint Icon,
        ScreenPoint Label, ScreenPoint Empty, ScreenPoint Outside);

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct ListViewItem
        {
            public uint Mask;
            public int Item, SubItem;
            public uint State, StateMask;
            [MarshalAs(UnmanagedType.LPWStr)] public string Text;
            public int TextMax, Image;
            public nint Param;
            public int Indent, GroupId;
            public uint Columns;
            public nint ColumnIndices, ColumnFormats;
            public int Group;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern nint CreateWindowEx(uint exStyle, string className, string name,
            uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        internal static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
        internal static extern nint InsertItem(nint hwnd, uint message, nint wParam, ref ListViewItem item);
        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        internal static extern nint GetItemRect(nint hwnd, uint message, nint wParam, ref Rect rect);
        [DllImport("user32.dll")]
        internal static extern bool ClientToScreen(nint hwnd, ref Point point);
        [DllImport("user32.dll")]
        internal static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);
    }
}

using System.Runtime.InteropServices;
using MouseEnhance.Models;

namespace MouseEnhance.Services.Features;

/// <summary>Cross-process-safe list-view hit testing. Never call from an input hook.</summary>
internal static class DesktopIconHitTest
{
    private const uint ObjIdClient = 0xFFFFFFFC;

    public static bool IsEmpty(nint listView, ScreenPoint point)
    {
        if (listView == 0 || !IsWindow(listView))
            return false;

        // Some accessibility proxies report CHILDID_SELF even outside the client.
        // Validate bounds independently before accepting a background hit.
        var clientPoint = new NativePoint { X = point.X, Y = point.Y };
        if (!ScreenToClient(listView, ref clientPoint) || !GetClientRect(listView, out var rect) ||
            clientPoint.X < rect.Left || clientPoint.X >= rect.Right ||
            clientPoint.Y < rect.Top || clientPoint.Y >= rect.Bottom)
            return false;

        IAccessibleHitTest? accessible = null;
        object? hit = null;
        try
        {
            var iid = typeof(IAccessibleHitTest).GUID;
            if (AccessibleObjectFromWindow(listView, ObjIdClient, ref iid, out accessible) != 0 ||
                accessible is null)
                return false;

            // Oleacc/COM marshals this call into the owning process. LVM_HITTEST
            // (WM_USER + 18) does NOT marshal a caller-local LVHITTESTINFO pointer.
            hit = accessible.HitTest(point.X, point.Y);

            // Only VT_I4 / CHILDID_SELF positively identifies empty client space.
            // Null (outside/unknown), child IDs and child objects must not toggle.
            return hit is int childId && childId == 0;
        }
        catch (COMException)
        {
            // Explorer may be closing/recreating its desktop. Fail closed.
            return false;
        }
        finally
        {
            if (hit is not null && Marshal.IsComObject(hit))
                Marshal.ReleaseComObject(hit);
            if (accessible is not null)
                Marshal.ReleaseComObject(accessible);
        }
    }

    // Dispatch-only projection: no hand-written IAccessible vtable or dependency
    // on Windows Forms. DISPID_ACC_HITTEST is defined by the MSAA type library.
    [ComImport]
    [Guid("618736E0-3C3D-11CF-810C-00AA00389B71")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    private interface IAccessibleHitTest
    {
        [DispId(-5017)]
        [return: MarshalAs(UnmanagedType.Struct)]
        object? HitTest(int x, int y);
    }

    [DllImport("oleacc.dll", ExactSpelling = true)]
    private static extern int AccessibleObjectFromWindow(
        nint hwnd, uint objectId, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IAccessibleHitTest? accessible);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(nint hwnd, ref NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint hwnd, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
}

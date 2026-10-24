using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

/// <summary>
/// Thin wrapper over the shell AppBar API. Excluded from coverage because every method is
/// an interop call whose behaviour is defined by the running shell; the rectangle
/// arithmetic it relies on lives in <see cref="ClassificationBanner.BannerGeometry"/>
/// and is covered there.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AppBarHelper
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    public enum ABMsg : int
    {
        ABM_NEW = 0,
        ABM_REMOVE = 1,
        ABM_QUERYPOS = 2,
        ABM_SETPOS = 3,
        ABM_GETSTATE = 4,
        ABM_GETTASKBARPOS = 5,
        ABM_ACTIVATE = 6,
        ABM_GETAUTOHIDEBAR = 7,
        ABM_SETAUTOHIDEBAR = 8,
        ABM_WINDOWPOSCHANGED = 9,
        ABM_SETSTATE = 10
    }

    public enum ABEdge : int
    {
        ABE_LEFT = 0,
        ABE_TOP = 1,
        ABE_RIGHT = 2,
        ABE_BOTTOM = 3
    }

    [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    /// <summary>
    /// Registers the window as an AppBar and negotiates its reserved area with the shell.
    /// Returns the rectangle the shell actually granted, in physical screen pixels. The
    /// caller must move the window to this rectangle, otherwise the reserved area and the
    /// visible window will not coincide.
    /// </summary>
    public static RECT RegisterBar(IntPtr hwnd, RECT desiredRect, ABEdge edge, int thickness)
    {
        APPBARDATA abd = new APPBARDATA();
        abd.cbSize = Marshal.SizeOf(abd);
        abd.hWnd = hwnd;
        abd.uEdge = (int)edge;
        abd.rc = desiredRect;

        // 1. Register the AppBar
        SHAppBarMessage((int)ABMsg.ABM_NEW, ref abd);

        // 2. Ask the shell where this bar may live. The shell rewrites abd.rc to exclude
        //    space already claimed by the taskbar and any other registered AppBars.
        SHAppBarMessage((int)ABMsg.ABM_QUERYPOS, ref abd);

        // 3. ABM_QUERYPOS only moves the docking edge; the opposite edge still holds the
        //    original value. Re-derive it so the bar keeps its intended thickness.
        abd.rc = ClassificationBanner.BannerGeometry.ApplyThickness(abd.rc, edge, thickness);

        // 4. Commit the final rectangle. The shell may adjust it once more.
        SHAppBarMessage((int)ABMsg.ABM_SETPOS, ref abd);

        return abd.rc;
    }

    /// <summary>
    /// Notifies the shell that the AppBar window has been moved or resized.
    /// </summary>
    public static void NotifyPosChanged(IntPtr hwnd)
    {
        APPBARDATA abd = new APPBARDATA();
        abd.cbSize = Marshal.SizeOf(abd);
        abd.hWnd = hwnd;
        SHAppBarMessage((int)ABMsg.ABM_WINDOWPOSCHANGED, ref abd);
    }

    public static void UnregisterBar(IntPtr hwnd)
    {
        APPBARDATA abd = new APPBARDATA();
        abd.cbSize = Marshal.SizeOf(abd);
        abd.hWnd = hwnd;
        SHAppBarMessage((int)ABMsg.ABM_REMOVE, ref abd);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
}

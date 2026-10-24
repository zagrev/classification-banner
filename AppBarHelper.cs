using System;
using System.Runtime.InteropServices;

public static class AppBarHelper
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
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

    public static void RegisterBar(IntPtr hwnd, RECT rect, ABEdge edge)
    {
        APPBARDATA abd = new APPBARDATA();
        abd.cbSize = Marshal.SizeOf(abd);
        abd.hWnd = hwnd;
        abd.uEdge = (int)edge;
        abd.rc = rect;

        // 1. Register the Appbar
        SHAppBarMessage((int)ABMsg.ABM_NEW, ref abd);

        // 2. Negotiate space & Set positioning
        SHAppBarMessage((int)ABMsg.ABM_QUERYPOS, ref abd);
        SHAppBarMessage((int)ABMsg.ABM_SETPOS, ref abd);
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
public const uint SWP_NOACTIVATE = 0x0010;
}

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace QuickTools;
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd,int id);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetClassName(IntPtr hwnd,StringBuilder name,int count);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFO info);
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct MONITORINFO { public int Size; public RECT Monitor,Work; public uint Flags; }
    internal static bool Ctrl => (GetAsyncKeyState(0x11)&0x8000)!=0;
    internal static string Class(IntPtr hwnd) { var s=new StringBuilder(256); GetClassName(hwnd,s,256); return s.ToString(); }
}

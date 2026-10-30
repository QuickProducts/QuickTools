using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace QuickTools;
internal sealed class RadialWindow : Window
{
    private GlassSurface? surface;
    private ToolAction[] actions=Array.Empty<ToolAction>();
    private ToolAction[] allActions=Array.Empty<ToolAction>();
    private int hovered=-1,page,fileCount;
    private long lastRelease;
    internal event Action<ToolAction>? Chosen;
    internal RadialWindow()
    {
        WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=Brushes.Transparent;
        ResizeMode=ResizeMode.NoResize; Topmost=true; ShowActivated=false; ShowInTaskbar=false;
        SourceInitialized+=(_,_)=>HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(NoActivate);
        PreviewMouseLeftButtonDown+=(_,e)=>e.Handled=true;
        PreviewMouseLeftButtonUp+=(_,e)=> { e.Handled=true; Native.GetCursorPos(out var point); ReleaseAt(point); };
    }
    private static IntPtr NoActivate(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    { if(message==0x21) { handled=true; return new IntPtr(3); } return IntPtr.Zero; }
    internal void Open(Native.POINT origin,ToolAction[] options,int count)
    {
        Hide(); allActions=options; page=0; fileCount=count; hovered=-1; lastRelease=0;
        double desired=GlassSurface.Center*2*UserSettings.Current.MenuScale;
        var hwnd=new WindowInteropHelper(this).EnsureHandle();
        Native.SetWindowPos(hwnd,new IntPtr(-1),origin.X,origin.Y,0,0,0x11);
        double dpi=Math.Max(96,Native.GetDpiForWindow(hwnd))/96.0;
        int pixels=(int)Math.Min(768,Math.Ceiling(desired*dpi));
        Width=Height=pixels/dpi;
        ShowPage(0);
        int x=origin.X-pixels/2,y=origin.Y-pixels/2;
        var monitor=new Native.MONITORINFO { Size=System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        if(Native.GetMonitorInfo(Native.MonitorFromWindow(hwnd,2),ref monitor))
        {
            pixels=Math.Min(pixels,Math.Min(monitor.Work.Right-monitor.Work.Left-12,monitor.Work.Bottom-monitor.Work.Top-12));
            Width=Height=pixels/dpi;
            x=Math.Clamp(x,monitor.Work.Left+6,Math.Max(monitor.Work.Left+6,monitor.Work.Right-pixels-6));
            y=Math.Clamp(y,monitor.Work.Top+6,Math.Max(monitor.Work.Top+6,monitor.Work.Bottom-pixels-6));
        }
        Native.SetWindowPos(hwnd,new IntPtr(-1),x,y,pixels,pixels,0x10); Show();
        BeginAnimation(OpacityProperty,null); Opacity=1;
        if(UserSettings.Current.Animations && Content is FrameworkElement content)
        {
            // WPF forbids a non-identity transform on Window itself.
            content.RenderTransformOrigin=new Point(.5,.5);
            var scale=new ScaleTransform(.96,.96); content.RenderTransform=scale;
            var duration=TimeSpan.FromMilliseconds(120); var ease=new CubicEase { EasingMode=EasingMode.EaseOut };
            content.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,duration));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(.96,1,duration) { EasingFunction=ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(.96,1,duration) { EasingFunction=ease });
        }
    }
    internal void Track(Native.POINT cursor)
    {
        if(!IsVisible || surface==null) return;
        var p=surface.PointFromScreen(new Point(cursor.X,cursor.Y));
        double x=p.X-GlassSurface.Center,y=p.Y-GlassSurface.Center;
        if(x*x+y*y>(GlassSurface.Radius+45)*(GlassSurface.Radius+45)) { Hide(); return; }
        hovered=surface.Hit(p); surface.Highlight(hovered);
    }
    private void ShowPage(int target)
    {
        page=target; hovered=-1; actions=MenuPages.Get(allActions,page);
        surface=new GlassSurface(actions,fileCount); surface.SetPage(page,MenuPages.Count(allActions));
        Content=new Viewbox { Child=surface };
    }
    internal void ReleaseAt(Native.POINT cursor)
    {
        if(!IsVisible || Environment.TickCount64-lastRelease<160) return;
        lastRelease=Environment.TickCount64;
        try { Track(cursor); Choose(); }
        catch(Exception ex) { Hide(); RuntimeLog.Write("Menu click",ex); }
    }
    internal bool Choose()
    {
        if(!IsVisible || hovered<0) return false;
        var a=actions[hovered];
        if(a.Engine=="page") { ShowPage(int.Parse(a.Arguments[0])); return true; }
        Hide(); Chosen?.Invoke(a); return true;
    }
}

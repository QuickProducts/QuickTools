using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
namespace QuickTools;
internal static class WindowLayout
{
    internal static Size Fit(double width,double height,double workWidth,double workHeight)
    {
        double scale=Math.Min(1,Math.Min(Math.Max(1,workWidth-24)/width,Math.Max(1,workHeight-24)/height));
        return new Size(width*scale,height*scale);
    }
    internal static void Attach(Window window,double designWidth,double designHeight)
    {
        var content=(FrameworkElement)window.Content; window.Content=null;
        content.Width=designWidth; content.Height=designHeight;
        window.Content=new Viewbox { Child=content,Stretch=Stretch.Uniform };
        window.MinWidth=240; window.MinHeight=180;
        void Apply()
        {
            var hwnd=new WindowInteropHelper(window).Handle;
            if(hwnd==IntPtr.Zero) return;
            var info=new Native.MONITORINFO { Size=Marshal.SizeOf<Native.MONITORINFO>() };
            if(!Native.GetMonitorInfo(Native.MonitorFromWindow(hwnd,2),ref info)) return;
            double dpi=Math.Max(96,Native.GetDpiForWindow(hwnd))/96.0;
            double width=(info.Work.Right-info.Work.Left)/dpi,height=(info.Work.Bottom-info.Work.Top)/dpi;
            Size fit=Fit(designWidth,designHeight,width,height);
            window.Width=fit.Width; window.Height=fit.Height;
            int pixelsX=(int)Math.Round(fit.Width*dpi),pixelsY=(int)Math.Round(fit.Height*dpi);
            Native.SetWindowPos(hwnd,IntPtr.Zero,info.Work.Left+(info.Work.Right-info.Work.Left-pixelsX)/2,info.Work.Top+(info.Work.Bottom-info.Work.Top-pixelsY)/2,pixelsX,pixelsY,0x14);
        }
        window.Loaded+=(_,_)=>Apply();
        window.SourceInitialized+=(_,_)=>HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)?.AddHook((IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled)=>
        { if(msg==0x2E0) window.Dispatcher.BeginInvoke(new Action(Apply),DispatcherPriority.Loaded); return IntPtr.Zero; });
    }
}

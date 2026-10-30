using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
namespace QuickTools;
internal static class UiRegressionTests
{
    internal static int Run(string report)
    {
        int result=1; var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        bool previous=UserSettings.Current.Animations;
        app.Startup+=async (_,_)=>
        {
            RadialWindow? menu=null;
            try
            {
                menu=new RadialWindow(); Native.GetCursorPos(out var cursor);
                var timings=new System.Collections.Generic.List<double>();
                for(int i=0;i<16;i++)
                {
                    UserSettings.Current.Animations=i%2==0;
                    var clock=Stopwatch.StartNew(); menu.Open(cursor,Actions.For(new[]{"sample.mp4"}),1); timings.Add(clock.Elapsed.TotalMilliseconds);
                    await Task.Delay(35); menu.UpdateLayout();
                    if(!menu.RenderTransform.Value.IsIdentity) throw new Exception("Window transform must stay identity");
                    if(menu.Width>768 || !double.IsFinite(menu.Height)) throw new Exception("Unbounded menu dimensions");
                    menu.Track(cursor); menu.Hide();
                }
                var welcome=new WelcomeWindow(); welcome.Show(); await Task.Delay(50);
                var helper=new System.Windows.Interop.WindowInteropHelper(welcome);
                var monitor=new Native.MONITORINFO { Size=System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
                Native.GetMonitorInfo(Native.MonitorFromWindow(helper.Handle,2),ref monitor);
                double dpi=Native.GetDpiForWindow(helper.Handle)/96.0;
                if(welcome.Width*dpi>monitor.Work.Right-monitor.Work.Left || welcome.Height*dpi>monitor.Work.Bottom-monitor.Work.Top) throw new Exception("Welcome window outside work area");
                welcome.Close(); menu.Close();
                ConversionHistory.Add(new(DateTime.Now,@"C:\Examples\clip.mp4","WebM",@"C:\Examples\clip_converted.webm",null));
                var history=new ConversionHistoryWindow(); history.Show(); await Task.Delay(30); history.Close();
                await File.WriteAllTextAsync(report,$"PASS: 16 native menu openings with animations on/off, identity Window transform, finite bounded size, real first-run window fits monitor. No global input hooks installed.\nMenu opening median: {timings.OrderBy(x=>x).ElementAt(timings.Count/2):0.0} ms; maximum including cold start: {timings.Max():0.0} ms.\nThis test does not synthesize Ctrl or automate other applications.\n"); result=0;
            }
            catch(Exception ex) { await File.WriteAllTextAsync(report,"FAIL: "+ex); menu?.Close(); }
            finally { UserSettings.Current.Animations=previous; app.Shutdown(); }
        };
        app.Run(); return result;
    }
}

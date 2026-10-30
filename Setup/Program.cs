using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
namespace QuickTools;
internal static class Program
{
    [STAThread] public static void Main(string[] args)
    {
        if(args.Length==2 && args[0]=="--self-test") { Environment.ExitCode=InstallerEngine.SelfTest(args[1]).GetAwaiter().GetResult(); return; }
        var app=new Application();
        var window=new SetupWindow();
        if(args.Length==2 && args[0]=="--render-preview")
        {
            var root=(FrameworkElement)window.Content; root.Measure(new Size(1000,690)); root.Arrange(new Rect(0,0,1000,690)); root.UpdateLayout();
            var image=new System.Windows.Media.Imaging.RenderTargetBitmap(1000,690,96,96,System.Windows.Media.PixelFormats.Pbgra32); image.Render(root);
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image)); using var file=File.Create(args[1]); encoder.Save(file); return;
        }
        if(args.Length==2 && args[0]=="--uninstall")
        {
            string root=Path.GetFullPath(args[1]);
            if(Environment.ProcessPath!.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            {
                string copy=Path.Combine(Path.GetTempPath(),"QuickTools-Uninstall-"+Guid.NewGuid().ToString("N")+".exe");
                File.Copy(Environment.ProcessPath,copy); var start=new ProcessStartInfo(copy) { UseShellExecute=true }; start.ArgumentList.Add("--uninstall"); start.ArgumentList.Add(root); Process.Start(start); return;
            }
            window.ConfigureUninstall(root);
        }
        app.Run(window);
    }
}

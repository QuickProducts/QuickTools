using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace QuickTools;
internal static class PreviewRenderer
{
    internal static void Render(string directory)
    {
        Directory.CreateDirectory(directory);
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        var settings=new SettingsWindow();
        var content=(FrameworkElement)settings.Content;
        content.Measure(new Size(1120,790)); content.Arrange(new Rect(0,0,1120,790)); content.UpdateLayout();
        Save(content,1120,790,Path.Combine(directory,"settings.png"));
        ConversionHistory.Add(new(System.DateTime.Now,@"C:\Examples\video.mp4","WebM",@"C:\Examples\video_converted.webm",null));
        var history=new ConversionHistoryWindow(); var historyRoot=(FrameworkElement)history.Content;
        historyRoot.Measure(new Size(900,620)); historyRoot.Arrange(new Rect(0,0,900,620)); historyRoot.UpdateLayout();
        Save(historyRoot,900,620,Path.Combine(directory,"results.png")); history.Close();
        var compactSettings=WindowLayout.Fit(1120,790,960,600);
        content.Measure(compactSettings); content.Arrange(new Rect(new Point(),compactSettings)); content.UpdateLayout();
        Save(content,(int)compactSettings.Width,(int)compactSettings.Height,Path.Combine(directory,"settings-compact.png"));
        foreach(var step in new[]{WelcomeStep.Welcome,WelcomeStep.Components,WelcomeStep.Installing,WelcomeStep.Trigger,WelcomeStep.Batch,WelcomeStep.Scripts,WelcomeStep.Style})
        {
            var welcome=new WelcomeWindow(); welcome.ShowPreviewStep(step); var root=(FrameworkElement)welcome.Content;
            root.Measure(new Size(1060,660)); root.Arrange(new Rect(0,0,1060,660)); root.UpdateLayout();
            Save(root,1060,660,Path.Combine(directory,"welcome-"+step.ToString().ToLowerInvariant()+".png")); welcome.Close();
            if(step==WelcomeStep.Welcome)
            {
                var size=WindowLayout.Fit(1060,660,960,600); root.Measure(size); root.Arrange(new Rect(new Point(),size)); root.UpdateLayout();
                Save(root,(int)size.Width,(int)size.Height,Path.Combine(directory,"welcome-compact.png"));
            }
        }
        foreach(var file in new[]{"demo.mp4","demo.png","demo.wav","demo.txt"})
        {
            var all=Actions.For(new[]{file}); var surface=new GlassSurface(MenuPages.Get(all,0),1); surface.SetPage(0,MenuPages.Count(all)); surface.Measure(new Size(440,440)); surface.Arrange(new Rect(0,0,440,440)); surface.UpdateLayout();
            if(file.EndsWith("mp4")) surface.Highlight(0);
            Save(surface,440,440,Path.Combine(directory,Path.GetExtension(file)[1..]+"-menu.png"));
        }
        settings.Close(); app.Shutdown();
    }
    private static void Save(Visual element,int width,int height,string path)
    {
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); bitmap.Render(element);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream=File.Create(path); encoder.Save(stream);
    }
}

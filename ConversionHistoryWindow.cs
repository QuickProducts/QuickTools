using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace QuickTools;
internal sealed class ConversionHistoryWindow : Window
{
    private readonly StackPanel rows=new();
    internal ConversionHistoryWindow()
    {
        Title="QuickTools — Задачи и результаты"; Width=900; Height=620; WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=Brush("#101923"); Foreground=Brush("#E1EFF2"); FontFamily=new FontFamily("Segoe UI");
        var panel=new DockPanel { Margin=new Thickness(26) };
        var heading=new StackPanel { Margin=new Thickness(0,0,0,20) };
        heading.Children.Add(new TextBlock { Text="Задачи и результаты",FontSize=24,FontWeight=FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text="Здесь указан исходник, точный результат или причина ошибки.",Foreground=Brush("#86A2B2"),Margin=new Thickness(0,9,0,0) });
        DockPanel.SetDock(heading,Dock.Top); panel.Children.Add(heading);
        var log=new Button { Content="Открыть журнал ошибок",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,14,0,0) };
        log.Click+=(_,_)=>OpenFolder(Path.GetDirectoryName(UserSettings.FilePath)!); DockPanel.SetDock(log,Dock.Bottom); panel.Children.Add(log);
        panel.Children.Add(new ScrollViewer { Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
        Content=panel; WindowLayout.Attach(this,900,620);
        ConversionHistory.Changed+=RefreshLater; Closed+=(_,_)=>ConversionHistory.Changed-=RefreshLater; Refresh();
    }
    private void RefreshLater() { if(!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(Refresh)); }
    private void Refresh()
    {
        rows.Children.Clear(); var entries=ConversionHistory.Snapshot();
        if(ConversionHistory.ActiveJobs>0) rows.Children.Add(new TextBlock { Text=$"В очереди или выполняется: {ConversionHistory.ActiveJobs}. Результат появится после завершения и проверки.",Foreground=Brush("#9BE3D0"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,15) });
        if(entries.Length==0) { rows.Children.Add(new TextBlock { Text="Завершённых задач пока нет. Во время конвертации файл результата ещё не появляется.",TextWrapping=TextWrapping.Wrap,Foreground=Brush("#8CA6B4"),Margin=new Thickness(0,15,0,0) }); return; }
        foreach(var entry in entries)
        {
            var details=new StackPanel(); bool ok=entry.Output!=null;
            details.Children.Add(new TextBlock { Text=(ok?"Готово":"Ошибка")+" · "+entry.Action+" · "+entry.Time.ToString("HH:mm:ss"),FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=Brush(ok?"#98DECC":"#EEB49B") });
            details.Children.Add(new TextBlock { Text="Исходник: "+entry.Source,TextWrapping=TextWrapping.Wrap,Foreground=Brush("#9BB4BF"),Margin=new Thickness(0,9,0,0) });
            details.Children.Add(new TextBlock { Text=ok?"Результат: "+entry.Output:entry.Error,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,9,0,0),LineHeight=19 });
            if(entry.UsedFallback) details.Children.Add(new TextBlock { Text="Папка исходника недоступна для записи. Использована папка QuickTools\\Converted.",TextWrapping=TextWrapping.Wrap,Foreground=Brush("#E6C393"),Margin=new Thickness(0,8,0,0) });
            var button=new Button { Content=ok?"Открыть папку результата":"Скопировать ошибку",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,13,0,0) };
            button.Click+=(_,_)=> { try { if(ok) OpenFolder(Path.GetDirectoryName(entry.Output!)!); else Clipboard.SetText(entry.Source+Environment.NewLine+entry.Error); } catch(Exception ex) { RuntimeLog.Write("History action",ex); } };
            details.Children.Add(button);
            rows.Children.Add(new Border { Child=details,Padding=new Thickness(18),CornerRadius=new CornerRadius(12),Background=Brush("#1B2935"),BorderBrush=Brush("#324753"),BorderThickness=new Thickness(1),Margin=new Thickness(0,0,8,14) });
        }
    }
    private static void OpenFolder(string folder) { Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute=true }); }
    private static SolidColorBrush Brush(string value)=>new((Color)ColorConverter.ConvertFromString(value));
}

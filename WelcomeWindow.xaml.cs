using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace QuickTools;
public partial class WelcomeWindow : Window
{
    private readonly FirstRunFlow flow;
    private CancellationTokenSource? installCancellation;
    private bool working;
    private int tourPage;
    internal bool Finished { get; private set; }
    internal event Action? Completed;
    public WelcomeWindow():this(false) { }
    internal WelcomeWindow(bool replay)
    {
        InitializeComponent(); flow=new FirstRunFlow(replay);
        WindowLayout.Attach(this,1060,660);
        BrandLogo.Source=BrandAssets.Logo; WelcomeBanner.Source=BrandAssets.Banner;
        Closing+=OnClosing; RenderStep();
    }
    private void DragTitle(object sender,MouseButtonEventArgs e) { if(e.ChangedButton==MouseButton.Left) DragMove(); }
    private void CloseWindow(object sender,RoutedEventArgs e)=>Close();
    private void OnClosing(object? sender,CancelEventArgs e)
    {
        if(working) { e.Cancel=true; installCancellation?.Cancel(); InstallMessage.Text="Отменяем загрузку…"; }
    }
    private async void Next(object sender,RoutedEventArgs e)
    {
        if(working) return;
        if(flow.Step==WelcomeStep.Installing) { flow.Retry(); RenderStep(); return; }
        if(flow.Step==WelcomeStep.Style)
        {
            bool previous=UserSettings.Current.FirstRunCompleted;
            try { UserSettings.Current.FirstRunCompleted=true; UserSettings.Current.Save(); }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { UserSettings.Current.FirstRunCompleted=previous; FinishError.Text="Не удалось сохранить настройки: "+ex.Message; return; }
            flow.Next(); Finished=true; Completed?.Invoke(); Close(); return;
        }
        flow.Next(); tourPage=0; RenderStep();
        if(flow.Step==WelcomeStep.Installing) await Prepare();
    }
    private void Back(object sender,RoutedEventArgs e) { if(!working) { flow.Back(); tourPage=0; RenderStep(); } }
    private void SkipMissing(object sender,RoutedEventArgs e) { if(!working) { flow.Prepared(); RenderStep(); } }
    private void RenderStep()
    {
        foreach(var element in new FrameworkElement[]{WelcomePanel,ComponentsPanel,InstallPanel,TourPanel,StylePanel}) element.Visibility=Visibility.Collapsed;
        Stage.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(UserSettings.Current.Animations?180:0)));
        BackButton.Visibility=flow.Step is WelcomeStep.Welcome or WelcomeStep.Installing or WelcomeStep.Trigger?Visibility.Collapsed:Visibility.Visible;
        NextButton.IsEnabled=true; BottomHint.Text="";
        var copy=flow.Step switch
        {
            WelcomeStep.Welcome=>("Меньше кликов. Больше свободы.","Подготовим инструменты и покажем, как ими пользоваться.","Начнём  →"),
            WelcomeStep.Components=>("Выберите свои инструменты.","Скачаем только нужное. Без магазинов, офисных пакетов и прав администратора.","Установить  →"),
            WelcomeStep.Installing=>("Готовим всё к работе.","Проверяем программы, скачиваем недостающие и проверяем пакеты.","Подготовка…"),
            WelcomeStep.Trigger=>("Один файл. Один Ctrl.","Удерживайте Ctrl над файлом в Проводнике или на рабочем столе.","Дальше  →"),
            WelcomeStep.Batch=>("Несколько файлов? Легко.","Выделите группу в Проводнике и нажмите Ctrl + Space.","Дальше  →"),
            WelcomeStep.Scripts=>("Больше форматов — через «Ещё».","TXT → BAT, CMD, PS1 и другие расширения доступны без дополнительных программ.","Выбрать стиль  →"),
            _=>("Теперь — по-вашему.","Осталось выбрать цвет. QuickTools готов работать в фоне.","Начать работу  →")
        };
        Heading.Text=copy.Item1; Description.Text=copy.Item2; NextButton.Content=copy.Item3;
        Eyebrow.Text=flow.Step<WelcomeStep.Trigger?"ПОДГОТОВКА QUICKTOOLS":"БЫСТРОЕ ЗНАКОМСТВО";
        switch(flow.Step)
        {
            case WelcomeStep.Welcome: WelcomePanel.Visibility=Visibility.Visible; BottomHint.Text="Без спешки. Всё по шагам."; break;
            case WelcomeStep.Components: ComponentsPanel.Visibility=Visibility.Visible; break;
            case WelcomeStep.Installing: InstallPanel.Visibility=Visibility.Visible; NextButton.IsEnabled=false; break;
            case WelcomeStep.Style: StylePanel.Visibility=Visibility.Visible; BuildColors(); break;
            default: TourPanel.Visibility=Visibility.Visible; BuildTour(); break;
        }
        BuildSteps();
    }
    private void BuildSteps()
    {
        StepList.Children.Clear(); int active=flow.Step switch { WelcomeStep.Welcome=>0,WelcomeStep.Components or WelcomeStep.Installing=>1,WelcomeStep.Style=>3,_=>2 };
        var names=new[]{("Добро пожаловать","Начнём знакомство"),("Подготовка","Нужные компоненты"),("Быстрый старт","Три простых действия"),("Ваш стиль","Готово к работе")};
        for(int i=0;i<names.Length;i++)
        {
            var row=new StackPanel { Margin=new Thickness(13),Orientation=Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text=i<active?"✓":(i+1).ToString("00"),FontSize=13,Width=30,Foreground=Brush(i<=active?"#A6E7D5":"#5E7C8E") });
            var text=new StackPanel(); text.Children.Add(new TextBlock { Text=names[i].Item1,Foreground=Brush(i==active?"#E6F6F3":"#8DAAB5") }); text.Children.Add(new TextBlock { Text=names[i].Item2,FontSize=9,Margin=new Thickness(0,5,0,0),Foreground=Brush("#688B9B") }); row.Children.Add(text);
            StepList.Children.Add(new Border { Child=row,CornerRadius=new CornerRadius(10),Background=Brush(i==active?"#304C50":"#00202D3B"),Margin=new Thickness(0,0,0,12) });
        }
    }
    private async Task Prepare()
    {
        working=true; ContinueOffline.Visibility=Visibility.Collapsed; installCancellation=new CancellationTokenSource(); var token=installCancellation.Token;
        var tools=new List<string>(); if(MediaToggle.IsChecked==true) tools.AddRange(new[]{"ffmpeg","magick"}); if(DocumentsToggle.IsChecked==true || PdfCreateToggle.IsChecked==true) tools.Add("pandoc"); if(PdfCreateToggle.IsChecked==true) tools.Add("typst"); if(PdfReadToggle.IsChecked==true) tools.Add("pdftotext");
        var rows=new Dictionary<string,TextBlock>(); InstallRows.Children.Clear();
        foreach(string tool in tools) { var row=new TextBlock { Text="○  "+DependencyManager.Package(tool),Foreground=Brush("#8CA9B7"),Margin=new Thickness(0,0,0,16),FontSize=12 }; rows[tool]=row; InstallRows.Children.Add(row); }
        int failed=0;
        try
        {
            for(int i=0;i<tools.Count;i++)
            {
                token.ThrowIfCancellationRequested(); string tool=tools[i]; int index=i;
                void Progress(string current,double percent,string message) { if(current==tool) Dispatcher.BeginInvoke(new Action(()=> { double value=(index+percent/100)*100/tools.Count; InstallBar.Value=value; InstallPercent.Text=$"{value:0}%"; InstallPhase.Text=DependencyManager.Package(tool); InstallMessage.Text=message; })); }
                DependencyManager.ProgressChanged+=Progress;
                try
                {
                    rows[tool].Text="◌  "+DependencyManager.Package(tool)+" — подготовка";
                    await DependencyManager.Ensure(tool,_=>{},token,true); rows[tool].Text="✓  "+DependencyManager.Package(tool)+" — готово"; rows[tool].Foreground=Brush("#9FE4CF");
                }
                catch(OperationCanceledException) { throw; }
                catch(Exception ex) { failed++; rows[tool].Text="!  "+DependencyManager.Package(tool)+" — не установлен"; rows[tool].Foreground=Brush("#E7BE96"); rows[tool].ToolTip=ex.Message; InstallMessage.Text=ex.Message; }
                finally { DependencyManager.ProgressChanged-=Progress; }
                InstallBar.Value=(i+1)*100.0/tools.Count; InstallPercent.Text=$"{InstallBar.Value:0}%";
            }
            if(failed==0) { flow.Prepared(); RenderStep(); }
            else { InstallPhase.Text="Часть компонентов недоступна"; NextButton.IsEnabled=true; NextButton.Content="Повторить"; ContinueOffline.Visibility=Visibility.Visible; }
        }
        catch(OperationCanceledException) { flow.Retry(); RenderStep(); }
        finally { working=false; installCancellation.Dispose(); installCancellation=null; }
    }
    private void BuildTour()
    {
        bool batch=flow.Step==WelcomeStep.Batch,script=flow.Step==WelcomeStep.Scripts;
        TourFile.Text=script?"my_script.txt":batch?"3 выбранных файла":"weekend_trip.mp4";
        TourFileType.Text=script?"Текстовый файл":batch?"Совместимые действия для группы":"Видеофайл";
        TourKeys.Text=batch?"Ctrl + Space":"Ctrl + наведение";
        TourHint.Text=script?"Выберите BAT. Это копия текста с другим расширением — содержимое не переписывается и не запускается.":batch?"Выберите формат для группы. Результаты появятся рядом с оригиналами.":"Наведите курсор на сектор и кликните. Отпустите Ctrl или нажмите Esc, чтобы закрыть меню.";
        TourFeedback.Text="";
        var all=Actions.For(new[]{script?"my_script.txt":"weekend_trip.mp4"}); var options=MenuPages.Get(all,tourPage);
        var surface=new GlassSurface(options,batch?3:1); surface.SetPage(tourPage,MenuPages.Count(all));
        TourMenu.Child=surface;
        surface.MouseMove+=(_,e)=> { var point=e.GetPosition(surface); surface.Highlight(surface.Hit(point)); surface.MoveLight(point); };
        surface.MouseLeave+=(_,_)=>surface.Highlight(-1);
        surface.MouseLeftButtonUp+=(_,e)=>
        {
            int index=surface.Hit(e.GetPosition(surface)); if(index<0) return; var action=options[index];
            if(action.Engine=="page") { tourPage=int.Parse(action.Arguments[0]); BuildTour(); }
            else TourFeedback.Text=script && action.Engine=="copytext"?$"✓ Получится my_script_converted.{action.Extension}":$"✓ Выбрано: {action.Label}. В настоящем меню запустится действие.";
        };
    }
    private void BuildColors()
    {
        Colors.Children.Clear();
        foreach(string color in new[]{"#8DDDD0","#B6A5FA","#91BFFF","#F0BA91"})
        {
            var button=new Button { Content=new System.Windows.Shapes.Ellipse { Width=27,Height=27,Fill=Brush(color) },Padding=new Thickness(8),Margin=new Thickness(0,0,10,0),Background=Brush(UserSettings.Current.Accent==color?"#486164":"#223740"),ToolTip=color };
            button.Click+=(_,_)=> { UserSettings.Current.Accent=color; BuildColors(); }; Colors.Children.Add(button);
        }
        StyleMenu.Child=new GlassSurface(MenuPages.Get(Actions.For(new[]{"demo.mp4"}),0),1);
    }
    internal void ShowPreviewStep(WelcomeStep step) { while(flow.Step<step) { if(flow.Step==WelcomeStep.Installing) flow.Prepared(); else flow.Next(); } RenderStep(); }
    private static SolidColorBrush Brush(string value)=>new((Color)ColorConverter.ConvertFromString(value));
}

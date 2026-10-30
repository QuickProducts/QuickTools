using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace QuickTools;
public partial class SettingsWindow : Window
{
    private bool ready;
    private string sample="video";
    private int previewPage;
    private readonly Dictionary<string,Action> toolRefresh=new();
    private bool installing;
    private readonly DispatcherTimer saveTimer=new() { Interval=TimeSpan.FromMilliseconds(400) };
    internal event Action? Changed;
    public SettingsWindow()
    {
        InitializeComponent();
        WindowLayout.Attach(this,1120,790);
        BrandLogo.Source=BrandAssets.Logo;
        var s=UserSettings.Current;
        OpacitySlider.Value=s.GlassOpacity*100; BlurSlider.Value=s.BlurRadius; ScaleSlider.Value=s.MenuScale*100;
        AnimationsToggle.IsChecked=s.Animations; HoverToggle.IsChecked=s.HoverEnabled; NotificationsToggle.IsChecked=s.Notifications; DelaySlider.Value=s.HoverDelay;
        AutoInstallToggle.IsChecked=s.AutoInstallTools;
        saveTimer.Tick+=(_,_)=>Save(); Closing+=(_,_)=> { if(saveTimer.IsEnabled) Save(); };
        BuildSwatches(); BuildTools(); BuildActions(); ready=true; UpdateLabels(); RefreshPreview(); SelectPage("appearance");
        DependencyManager.StatusChanged+=OnInstallStatus;
        Closed+=(_,_)=>DependencyManager.StatusChanged-=OnInstallStatus;
    }
    private void DragTitle(object sender,MouseButtonEventArgs e) { if(e.ChangedButton==MouseButton.Left) DragMove(); }
    private void Minimize(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    private void CloseWindow(object sender,RoutedEventArgs e)=>Close();
    private void Navigate(object sender,RoutedEventArgs e)=>SelectPage((string)((Button)sender).Tag);
    private void SelectPage(string page)
    {
        AppearancePage.Visibility=page=="appearance"?Visibility.Visible:Visibility.Collapsed;
        BehaviorPage.Visibility=page=="behavior"?Visibility.Visible:Visibility.Collapsed;
        ActionsPage.Visibility=page=="actions"?Visibility.Visible:Visibility.Collapsed;
        ToolsPage.Visibility=page=="tools"?Visibility.Visible:Visibility.Collapsed;
        var titles=new Dictionary<string,(string,string)> { ["appearance"]=("Внешний вид","Лёгкое стекло. Ваш цвет. Ничего лишнего."),["behavior"]=("Управление","Настройте QuickTools под свой ритм."),["actions"]=("Действия","Только те инструменты, которыми вы пользуетесь."),["tools"]=("Конвертеры","Ваши файлы обрабатываются на этом компьютере.") };
        PageTitle.Text=titles[page].Item1; PageDescription.Text=titles[page].Item2;
        foreach(var button in new[]{AppearanceNav,BehaviorNav,ActionsNav,ToolsNav}) { bool active=(string)button.Tag==page; button.Background=Brush(active?"#243B3D":"#00121A23"); button.Foreground=Brush(active?"#ACEEE0":"#9CAEBE"); }
    }
    private void AppearanceChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if(!ready) return; var s=UserSettings.Current;
        s.GlassOpacity=OpacitySlider.Value/100; s.BlurRadius=BlurSlider.Value; s.MenuScale=ScaleSlider.Value/100;
        UpdateLabels(); RefreshPreview(); QueueSave();
    }
    private void BehaviorChanged(object sender,RoutedEventArgs e)
    {
        if(!ready) return; var s=UserSettings.Current;
        s.Animations=AnimationsToggle.IsChecked==true; s.HoverEnabled=HoverToggle.IsChecked==true; s.Notifications=NotificationsToggle.IsChecked==true; s.HoverDelay=(int)DelaySlider.Value;
        s.AutoInstallTools=AutoInstallToggle.IsChecked==true;
        UpdateLabels(); QueueSave();
    }
    private void UpdateLabels()
    {
        OpacityValue.Text=$"{OpacitySlider.Value:0}%"; BlurValue.Text=$"{BlurSlider.Value:0}"; ScaleValue.Text=$"{ScaleSlider.Value:0}%"; DelayValue.Text=$"{DelaySlider.Value:0} мс";
    }
    private void BuildSwatches()
    {
        Swatches.Children.Clear();
        foreach(string color in new[]{"#8DDDD0","#B6A5FA","#91BFFF","#F0BA91"})
        {
            var circle=new System.Windows.Shapes.Ellipse { Width=23,Height=23,Fill=Brush(color),Stroke=Brushes.White,StrokeThickness=UserSettings.Current.Accent==color?2:0 };
            var button=new Button { Content=circle,Padding=new Thickness(5),Margin=new Thickness(0,0,8,0),Background=Brush(UserSettings.Current.Accent==color?"#3A4B55":"#00202C38"),ToolTip=color,Tag=color };
            button.Click+=(_,_)=> { UserSettings.Current.Accent=color; BuildSwatches(); RefreshPreview(); QueueSave(); };
            Swatches.Children.Add(button);
        }
    }
    private void RefreshPreview()
    {
        string file=sample switch { "image"=>"morning_light.png","audio"=>"favorite_track.wav","text"=>"notes.md",_=>"weekend_trip.mp4" };
        var all=Actions.VisibleFor(new[]{file});
        previewPage=Math.Clamp(previewPage,0,MenuPages.Count(all)-1);
        var options=MenuPages.Get(all,previewPage);
        SampleFile.Text=file+" · "+(previewPage+1)+" / "+MenuPages.Count(all);
        var surface=new GlassSurface(options,1,PreviewBackground()); surface.SetPage(previewPage,MenuPages.Count(all));
        var frame=new Grid { Width=520,Height=520,Background=Brushes.Transparent };
        surface.HorizontalAlignment=HorizontalAlignment.Center; surface.VerticalAlignment=VerticalAlignment.Center;
        double scale=UserSettings.Current.MenuScale;
        surface.RenderTransformOrigin=new Point(.5,.5); surface.RenderTransform=new ScaleTransform(scale,scale);
        frame.Children.Add(surface); PreviewHost.Child=frame;
        frame.MouseMove+=(_,e)=> { var point=e.GetPosition(surface); surface.Highlight(surface.Hit(point)); surface.MoveLight(point); };
        frame.MouseLeave+=(_,_)=>surface.Highlight(-1);
        frame.MouseLeftButtonUp+=(_,e)=> { int hit=surface.Hit(e.GetPosition(surface)); if(hit>=0 && options[hit].Engine=="page") { previewPage=int.Parse(options[hit].Arguments[0]); RefreshPreview(); } };
    }
    private static ImageSource PreviewBackground()
    {
        var drawing=new DrawingGroup();
        using(var dc=drawing.Open())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(49,85,82),Color.FromRgb(78,57,98),35),null,new Rect(0,0,440,440));
            dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb(80,149,198,189)),55),Geometry.Parse("M-60,330 C100,-50 230,520 500,40"));
        }
        return new DrawingImage(drawing);
    }
    private void ChangeSample(object sender,RoutedEventArgs e) { sample=(string)((Button)sender).Tag; previewPage=0; RefreshPreview(); }
    private void BuildTools()
    {
        foreach(var (name,description) in new[]{("ffmpeg","Видео и аудио"),("magick","Изображения и иконки"),("pandoc","Документы и электронные книги"),("typst","Создание PDF из документов"),("pdftotext","Извлечение текста из PDF")})
        {
            var panel=new StackPanel { Margin=new Thickness(0,0,0,22) };
            panel.Children.Add(new TextBlock { Text=DependencyManager.Package(name),FontWeight=FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text=description,Foreground=Brush("#8B99AC"),FontSize=10,Margin=new Thickness(0,5,0,9) });
            var row=new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var input=new TextBox { Text=UserSettings.Current.ToolPaths.GetValueOrDefault(name,""),FontSize=10,ToolTip="Полный путь к "+name+".exe",VerticalContentAlignment=VerticalAlignment.Center };
            var browse=new Button { Content="…",Padding=new Thickness(10,7,10,7),Margin=new Thickness(6,0,0,0),ToolTip="Выбрать "+name+".exe" }; Grid.SetColumn(browse,1); row.Children.Add(input); row.Children.Add(browse); panel.Children.Add(row);
            var status=new TextBlock { FontSize=10,Margin=new Thickness(0,7,0,0),TextWrapping=TextWrapping.Wrap };
            void Update() { var path=ConversionService.Locate(name); status.Text=path==null?"○  Не найден":"●  Готов к работе"; status.Foreground=Brush(path==null?"#DEB487":"#8DDDD0"); status.ToolTip=path; }
            void Apply() { UserSettings.Current.ToolPaths[name]=input.Text.Trim().Trim('"'); Update(); QueueSave(); }
            input.LostKeyboardFocus+=(_,_)=>Apply();
            browse.Click+=(_,_)=> { var dialog=new Microsoft.Win32.OpenFileDialog { Filter="Программы (*.exe)|*.exe",Title="Выберите "+name+".exe" }; if(dialog.ShowDialog(this)==true) { input.Text=dialog.FileName; Apply(); } };
            Update(); panel.Children.Add(status); ToolRows.Children.Add(panel);
            toolRefresh[name]=Update;
            var install=new Button { Content="Установить автоматически",FontSize=10,Padding=new Thickness(8),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,9,0,0) };
            install.Click+=async (_,_)=>
            {
                install.IsEnabled=false;
                try { await DependencyManager.Ensure(name,s=>Dispatcher.BeginInvoke(new Action(()=>InstallStatus.Text=s)),CancellationToken.None,true); }
                catch(Exception ex) { InstallStatus.Text=ex.Message; }
                finally { install.IsEnabled=true; Update(); }
            };
            panel.Children.Add(install);
        }
    }
    private void BuildActions()
    {
        ActionRows.Children.Clear();
        var all=new[]{"demo.mp4","demo.wav","demo.png","demo.docx","demo.pdf","demo.txt"}.SelectMany(f=>Actions.For(new[]{f})).GroupBy(Actions.Id).Select(g=>g.First());
        string search=ActionSearch?.Text.Trim()??"";
        all=all.Where(a=>a.Label.Contains(search,StringComparison.OrdinalIgnoreCase));
        foreach(var action in all)
        {
            string id=Actions.Id(action);
            var row=new Grid { Margin=new Thickness(0,0,0,17) };
            var text=new StackPanel { Orientation=Orientation.Horizontal };
            text.Children.Add(new VectorIcon(VectorIcon.For(action),18) { Ink=Brush("#A9BEC9"),Margin=new Thickness(0,0,12,0) });
            text.Children.Add(new TextBlock { Text=action.Label+(action.Engine=="copytext"?" · копия текста":""),VerticalAlignment=VerticalAlignment.Center }); row.Children.Add(text);
            var toggle=new CheckBox { HorizontalAlignment=HorizontalAlignment.Right,IsChecked=!UserSettings.Current.HiddenActions.Contains(id) }; System.Windows.Automation.AutomationProperties.SetName(toggle,"Показывать "+action.Label);
            toggle.Click+=(_,_)=> { if(toggle.IsChecked==true) UserSettings.Current.HiddenActions.Remove(id); else if(!UserSettings.Current.HiddenActions.Contains(id)) UserSettings.Current.HiddenActions.Add(id); RefreshPreview(); QueueSave(); };
            row.Children.Add(toggle); ActionRows.Children.Add(row);
        }
    }
    private void SearchActions(object sender,TextChangedEventArgs e) { if(ready) BuildActions(); }
    private void OnInstallStatus(string tool,string message)=>Dispatcher.BeginInvoke(new Action(()=> { InstallStatus.Text=message; if(toolRefresh.TryGetValue(tool,out var refresh)) refresh(); }));
    private async void InstallAllTools(object sender,RoutedEventArgs e)
    {
        if(installing) return; installing=true; InstallAllButton.IsEnabled=false; InstallProgress.Visibility=Visibility.Visible;
        try
        {
            bool success=await DependencyManager.InstallAll(s=>Dispatcher.BeginInvoke(new Action(()=>InstallStatus.Text=s)),CancellationToken.None);
            InstallStatus.Text=success?"Всё готово. Все конвертеры найдены.":"Часть программ не установлена. Повторите установку или укажите путь к EXE.";
        }
        catch(Exception ex) { InstallStatus.Text=ex.Message; }
        finally { installing=false; InstallAllButton.IsEnabled=true; InstallProgress.Visibility=Visibility.Collapsed; foreach(var refresh in toolRefresh.Values) refresh(); }
    }
    private void ResetAppearance(object sender,RoutedEventArgs e)
    {
        var s=UserSettings.Current; s.Accent="#8DDDD0"; OpacitySlider.Value=42; BlurSlider.Value=18; ScaleSlider.Value=100; s.Animations=true; AnimationsToggle.IsChecked=true;
        BuildSwatches(); RefreshPreview(); QueueSave();
    }
    private void QueueSave() { if(!ready) return; SaveStatus.Text="Сохранение…"; saveTimer.Stop(); saveTimer.Start(); Changed?.Invoke(); }
    private void Save()
    {
        saveTimer.Stop();
        try { UserSettings.Current.Save(); SaveStatus.Text="●  Все изменения сохранены"; SaveStatus.Foreground=Brush("#8EB9AD"); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { SaveStatus.Text="Не удалось сохранить настройки"; SaveStatus.Foreground=Brush("#E9B094"); SaveStatus.ToolTip=ex.Message; }
    }
    private static SolidColorBrush Brush(string color)=>new((Color)ColorConverter.ConvertFromString(color));
}


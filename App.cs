using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;
using Forms=System.Windows.Forms;

namespace QuickTools;
internal sealed class App : Application
{
    private readonly RadialWindow menu=new();
    private readonly ConversionService converter=new();
    private SettingsWindow? settingsWindow;
    private ConversionHistoryWindow? historyWindow;
    private WelcomeWindow? welcomeWindow;
    private bool inputStarted;
    private HwndSource? hotkeySource;
    private bool hotkeyRegistered,hotkeyUnavailable,mouseDown;
    private long lastContextCheck;
    private readonly ShellQueryService shellQueries=new();
    private CancellationTokenSource? queryCancellation;
    private bool exiting;
    private readonly EventWaitHandle openSettings=new(false,EventResetMode.AutoReset,"Local\\QuickTools.OpenSettings");
    private readonly EventWaitHandle exitSignal=new(false,EventResetMode.AutoReset,"Local\\QuickTools.Exit");
    private System.Drawing.Icon? trayIcon;
    private Forms.NotifyIcon tray=null!;
    private readonly DispatcherTimer timer=new() { Interval=TimeSpan.FromMilliseconds(35) };
    private bool enabled=true,ctrl,requested,busy;
    private DateTime stationary=DateTime.UtcNow;
    private Native.POINT last;
    private int generation;
    private string[] files=Array.Empty<string>();
    [STAThread] public static void Main(string[] args)
    {
        if(args.Length==4 && args[0]=="--resolve-shell")
        {
            ShellReply reply;
            try { var point=new Native.POINT { X=int.Parse(args[1]),Y=int.Parse(args[2]) }; reply=new(ShellResolver.Resolve(point,args[3]=="selection")); }
            catch(Exception ex) { reply=new(Array.Empty<string>(),ex.Message); }
            Console.Out.Write(System.Text.Json.JsonSerializer.Serialize(reply)); return;
        }
        if(args.Length==2 && args[0]=="--install-tools")
        {
            var log=new System.Collections.Generic.List<string>();
            bool success=DependencyManager.InstallAll(s=>log.Add(s),CancellationToken.None).GetAwaiter().GetResult();
            foreach(string tool in DependencyManager.Tools) log.Add(tool+": "+(ConversionService.Locate(tool)??"NOT FOUND"));
            System.IO.File.WriteAllLines(args[1],log); Environment.ExitCode=success?0:1; return;
        }
        if(args.Length==2 && args[0]=="--self-test") { ConversionHistory.PersistenceEnabled=false; RuntimeLog.Enabled=false; Environment.ExitCode=SmokeTests.Run(args[1]).GetAwaiter().GetResult(); return; }
        if(args.Length==2 && args[0]=="--render-preview") { ConversionHistory.PersistenceEnabled=false; RuntimeLog.Enabled=false; PreviewRenderer.Render(args[1]); return; }
        if(args.Length==2 && args[0]=="--ui-regression") { ConversionHistory.PersistenceEnabled=false; Environment.ExitCode=UiRegressionTests.Run(args[1]); return; }
        if(args.Length==2 && args[0]=="--media-regression") { ConversionHistory.PersistenceEnabled=false; RuntimeLog.Enabled=false; Environment.ExitCode=MediaRegressionTests.Run(args[1]).GetAwaiter().GetResult(); return; }
        if(args.Length==2 && args[0]=="--image-regression") { ConversionHistory.PersistenceEnabled=false; RuntimeLog.Enabled=false; Environment.ExitCode=ImageRegressionTests.Run(args[1]).GetAwaiter().GetResult(); return; }
        using var mutex=new Mutex(true,"Local\\QuickTools.Radial",out bool first);
        if(!first) { if(EventWaitHandle.TryOpenExisting("Local\\QuickTools.OpenSettings",out var signal)) { signal.Set(); signal.Dispose(); } return; }
        new App().Run();
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); ShutdownMode=ShutdownMode.OnExplicitShutdown;
        trayIcon=CreateTrayIcon();
        tray=new Forms.NotifyIcon { Icon=trayIcon,Text="QuickTools · Ctrl + наведение / Ctrl+Space",Visible=true };
        var context=new Forms.ContextMenuStrip();
        context.Items.Add("Настройки QuickTools",null,(_,_)=>ShowSettings());
        context.Items.Add("Задачи и результаты",null,(_,_)=>ShowHistory());
        context.Items.Add("Знакомство с QuickTools",null,(_,_)=>ShowWelcome(true));
        context.Items.Add(new Forms.ToolStripSeparator());
        var pause=new Forms.ToolStripMenuItem("Пауза");
        pause.Click+=(_,_)=> { enabled=!enabled; menu.Hide(); generation++; pause.Checked=!enabled; }; context.Items.Add(pause);
        context.Items.Add("Проверить конвертеры",null,(_,_)=>Notify(string.Join("\n",Array.ConvertAll(DependencyManager.Tools,t=>t+": "+(ConversionService.Locate(t)??"не найден")))));
        context.Items.Add("Выход",null,(_,_)=>Shutdown()); tray.ContextMenuStrip=context;
        tray.DoubleClick+=(_,_)=>ShowSettings();
        tray.BalloonTipClicked+=(_,_)=>ShowHistory();
        menu.Chosen+=async action=>
        {
            try { var snapshot=files; if(action.Engine=="clipboard") { Clipboard.SetText(string.Join(Environment.NewLine,snapshot)); Notify("Пути скопированы"); } else { var result=await converter.Run(snapshot,action,Notify); if(result.Errors.Length>0 && !exiting) ShowHistory(); } }
            catch(Exception ex) { RuntimeLog.Write("Action",ex); Notify(ex.Message); }
        };
        timer.Tick+=(_,_)=> { try { Tick(); } catch(Exception ex) { menu.Hide(); RuntimeLog.Write("Input timer",ex); } }; timer.Start();
        if(!UserSettings.Current.FirstRunCompleted || Array.IndexOf(e.Args,"--onboarding")>=0) ShowWelcome(false);
        else { StartInput(); if(Array.IndexOf(e.Args,"--background")<0) ShowSettings(); }
    }
    private void StartInput()
    {
        if(inputStarted) return;
        hotkeySource=new HwndSource(new HwndSourceParameters("QuickTools Hotkey") { ParentWindow=new IntPtr(-3),Width=0,Height=0,WindowStyle=0 });
        hotkeySource.AddHook(HotkeyMessage); inputStarted=true;
    }
    private IntPtr HotkeyMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled)
    {
        if(message==0x312 && w.ToInt32()==1)
        {
            handled=true;
            if(enabled && welcomeWindow==null && settingsWindow?.IsActive!=true) Request(true);
        }
        return IntPtr.Zero;
    }
    private void UpdateHotkey()
    {
        if(hotkeySource==null || Environment.TickCount64-lastContextCheck<200) return;
        lastContextCheck=Environment.TickCount64;
        bool shouldRegister=enabled && welcomeWindow==null && settingsWindow?.IsActive!=true && ShellQueryService.IsShellTarget(default,true);
        if(shouldRegister && !hotkeyRegistered && !hotkeyUnavailable)
        {
            hotkeyRegistered=Native.RegisterHotKey(hotkeySource.Handle,1,0x4002,0x20);
            if(!hotkeyRegistered) { hotkeyUnavailable=true; Notify("Ctrl + Space занят другой программой. Доступно Ctrl + наведение."); }
        }
        else if(!shouldRegister && hotkeyRegistered) { Native.UnregisterHotKey(hotkeySource.Handle,1); hotkeyRegistered=false; }
    }
    private void Tick()
    {
        if(exitSignal.WaitOne(0)) { Shutdown(); return; }
        if(openSettings.WaitOne(0)) ShowSettings();
        UpdateHotkey();
        if(welcomeWindow!=null || !inputStarted) { menu.Hide(); return; }
        bool down=Native.Ctrl;
        timer.Interval=TimeSpan.FromMilliseconds(down?35:120);
        if(!down) { if(ctrl) { generation++; queryCancellation?.Cancel(); } ctrl=false; requested=false; menu.Hide(); mouseDown=false; return; }
        if((Native.GetAsyncKeyState(0x1B)&0x8000)!=0) { menu.Hide(); queryCancellation?.Cancel(); requested=true; generation++; return; }
        Native.GetCursorPos(out var p);
        if(!ctrl) { stationary=DateTime.UtcNow; last=p; } ctrl=true;
        bool button=(Native.GetAsyncKeyState(1)&0x8000)!=0;
        bool released=mouseDown && !button; mouseDown=button;
        if(menu.IsVisible) { menu.Track(p); if(released) menu.ReleaseAt(p); return; }
        if(Math.Abs(p.X-last.X)>5 || Math.Abs(p.Y-last.Y)>5) { stationary=DateTime.UtcNow; last=p; }
        if(enabled && settingsWindow?.IsActive!=true && UserSettings.Current.HoverEnabled && !requested && !busy && (DateTime.UtcNow-stationary).TotalMilliseconds>UserSettings.Current.HoverDelay) Request(false);
    }
    private async void Request(bool selection)
    {
        if(busy || !enabled || !Native.Ctrl || welcomeWindow!=null) return;
        Native.GetCursorPos(out var p);
        if(!ShellQueryService.IsShellTarget(p,selection)) return;
        requested=true; busy=true; int version=++generation;
        queryCancellation?.Dispose(); var cancel=new CancellationTokenSource(); queryCancellation=cancel;
        try
        {
            var reply=await shellQueries.Resolve(p,selection,cancel.Token);
            if(exiting || cancel.IsCancellationRequested || !enabled || welcomeWindow!=null || !Native.Ctrl || version!=generation) return;
            Native.GetCursorPos(out var current);
            if(!ShellQueryService.IsShellTarget(current,selection)) return;
            if(Math.Abs(current.X-p.X)>16 || Math.Abs(current.Y-p.Y)>16) return;
            if(reply.Paths.Length>0) { files=reply.Paths; var options=Actions.VisibleFor(files); if(options.Length>0) menu.Open(p,options,files.Length); else Notify("Все действия скрыты. Включите их в настройках."); }
            else if(selection) Notify(reply.Error??"В Проводнике не найдены выделенные локальные файлы.");
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) { RuntimeLog.Write("Shell/menu",ex); if(!exiting) Notify("Не удалось открыть меню: "+ex.Message); }
        finally { busy=false; }
    }
    private void ShowSettings()
    {
        if(welcomeWindow!=null) { welcomeWindow.Activate(); return; }
        menu.Hide(); generation++;
        if(settingsWindow==null)
        {
            settingsWindow=new SettingsWindow(); settingsWindow.Changed+=()=> { menu.Hide(); generation++; };
            settingsWindow.Closed+=(_,_)=>settingsWindow=null;
        }
        settingsWindow.Show(); if(settingsWindow.WindowState==WindowState.Minimized) settingsWindow.WindowState=WindowState.Normal; settingsWindow.Activate();
    }
    private void ShowHistory()
    {
        if(exiting) return;
        menu.Hide(); generation++;
        if(historyWindow==null) { historyWindow=new ConversionHistoryWindow(); historyWindow.Closed+=(_,_)=>historyWindow=null; }
        historyWindow.Show(); if(historyWindow.WindowState==WindowState.Minimized) historyWindow.WindowState=WindowState.Normal; historyWindow.Activate();
    }
    private void ShowWelcome(bool replay)
    {
        menu.Hide(); generation++;
        if(welcomeWindow!=null) { welcomeWindow.Activate(); return; }
        settingsWindow?.Hide();
        var window=new WelcomeWindow(replay); welcomeWindow=window;
        window.Closed+=(_,_)=>
        {
            welcomeWindow=null;
            if(window.Finished) { StartInput(); Notify("QuickTools готов. Удерживайте Ctrl над файлом."); }
            else if(!inputStarted && !exiting) Shutdown();
        };
        window.Show(); window.Activate();
    }
    private void Notify(string text)=>Dispatcher.BeginInvoke(new Action(()=> { if(!UserSettings.Current.Notifications) return; tray.BalloonTipTitle="QuickTools"; tray.BalloonTipText=text.Length>250?text[..250]:text; tray.ShowBalloonTip(3500); }));
    private static System.Drawing.Icon CreateTrayIcon()
    {
        using var stream=GetResourceStream(new Uri("pack://application:,,,/assets/QuickTools.ico")).Stream;
        using var icon=new System.Drawing.Icon(stream,32,32); return (System.Drawing.Icon)icon.Clone();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        exiting=true;
        timer.Stop(); generation++; queryCancellation?.Cancel(); shellQueries.Dispose();
        if(hotkeySource!=null) { if(hotkeyRegistered) Native.UnregisterHotKey(hotkeySource.Handle,1); hotkeySource.Dispose(); }
        converter.Dispose(); tray?.Dispose(); trayIcon?.Dispose(); settingsWindow?.Close(); historyWindow?.Close(); welcomeWindow?.Close(); openSettings.Dispose(); exitSignal.Dispose(); menu.Close(); base.OnExit(e);
    }
}

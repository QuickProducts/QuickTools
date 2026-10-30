using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
namespace QuickTools;
public partial class SetupWindow : Window
{
    private bool working,finished;
    private string? installed,uninstallRoot;
    public SetupWindow()
    {
        InitializeComponent(); InstallPath.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","QuickTools");
        WindowLayout.Attach(this,1000,690);
        BrandLogo.Source=BrandAssets.Logo;
        BrandBackdrop.Fill=new System.Windows.Media.ImageBrush(BrandAssets.Banner) {
            Viewbox=new Rect(.84,0,.16,1),ViewboxUnits=System.Windows.Media.BrushMappingMode.RelativeToBoundingBox,
            Stretch=System.Windows.Media.Stretch.UniformToFill,AlignmentX=System.Windows.Media.AlignmentX.Right };
        Hero.Child=new System.Windows.Controls.Image { Source=BrandAssets.Logo,Width=340,Height=340,Stretch=System.Windows.Media.Stretch.Uniform };
        Closing+=(_,e)=> { if(working) e.Cancel=true; };
    }
    internal void ConfigureUninstall(string root)
    {
        uninstallRoot=root; OptionsPanel.Visibility=Visibility.Collapsed; DonePanel.Visibility=Visibility.Visible;
        Heading.Text="Удалить QuickTools?"; Description.Text="Это займёт несколько секунд."; StepLabel.Text="УДАЛЕНИЕ";
        DoneTitle.Text="Спасибо, что попробовали."; DoneDescription.Text="Приложение и его ярлыки будут удалены. Настройки и скачанные конвертеры сохранятся."; PrimaryButton.Content="Удалить QuickTools";
    }
    private void DragTitle(object sender,MouseButtonEventArgs e) { if(e.ChangedButton==MouseButton.Left) DragMove(); }
    private void CloseSetup(object sender,RoutedEventArgs e) { if(!working) Close(); }
    private void Browse(object sender,RoutedEventArgs e)
    {
        var dialog=new Microsoft.Win32.OpenFolderDialog { Title="Папка QuickTools" };
        if(dialog.ShowDialog(this)==true) InstallPath.Text=Path.Combine(dialog.FolderName,"QuickTools");
    }
    private async void PrimaryClick(object sender,RoutedEventArgs e)
    {
        if(finished)
        {
            if(installed!=null)
            {
                bool running=Process.GetProcessesByName("QuickTools").Any(p=> { try { return !p.HasExited; } finally { p.Dispose(); } });
                if(running)
                {
                    Warnings.Text="Предыдущая версия QuickTools ещё работает. Закройте её через «Выход» в трее и нажмите кнопку снова.";
                    return;
                }
                var start=new ProcessStartInfo(Path.Combine(installed,"QuickTools.exe")) { UseShellExecute=true }; start.ArgumentList.Add("--onboarding"); Process.Start(start);
            }
            Close(); return;
        }
        if(working) return;
        if(uninstallRoot!=null) { await Remove(); return; }
        string root;
        try { root=InstallerEngine.ValidateRoot(InstallPath.Text); }
        catch(Exception ex) { Description.Text=ex.Message; return; }
        working=true; PrimaryButton.IsEnabled=false; PrimaryButton.Content="Устанавливаем…";
        OptionsPanel.Visibility=Visibility.Collapsed; ProgressPanel.Visibility=Visibility.Visible; Heading.Text="Устраивайтесь удобнее."; Description.Text="Мы подготовим приложение и нужные инструменты."; StepLabel.Text="02 / УСТАНАВЛИВАЕМ";
        var warnings=new List<string>();
        void Report(double value,string message)=>Dispatcher.Invoke(()=> { Progress.Value=value; Percent.Text=$"{value:0}%"; Phase.Text=message; });
        try
        {
            installed=await InstallerEngine.Extract(root,(v,m)=>Report(v*.25,m),CancellationToken.None);
            Report(25,"Создаём ярлыки…"); InstallerEngine.Register(root,installed,DesktopShortcut.IsChecked==true,StartShortcut.IsChecked==true);
            var tools=new List<string>(); if(MediaTools.IsChecked==true) tools.AddRange(new[]{"ffmpeg","magick"}); if(PdfTools.IsChecked==true) tools.Add("pdftotext"); if(OfficeTools.IsChecked==true) tools.AddRange(new[]{"pandoc","typst"});
            for(int i=0;i<tools.Count;i++)
            {
                string tool=tools[i]; int index=i;
                void OnProgress(string current,double percent,string message) { if(current==tool) Report(25+(index+percent/100)*70/tools.Count,message); }
                DependencyManager.ProgressChanged+=OnProgress;
                try
                {
                    Details.Text=$"Компонент {i+1} из {tools.Count}: {DependencyManager.Package(tool)}";
                    await DependencyManager.Ensure(tool,s=>Dispatcher.BeginInvoke(new Action(()=>Details.Text=s)),CancellationToken.None,true);
                }
                catch(Exception ex) { warnings.Add(DependencyManager.Package(tool)+": "+ex.Message); }
                finally { DependencyManager.ProgressChanged-=OnProgress; }
            }
            Report(100,"Готово"); working=false; finished=true; PrimaryButton.IsEnabled=true; PrimaryButton.Content="Перейти к знакомству  →";
            ProgressPanel.Visibility=Visibility.Collapsed; DonePanel.Visibility=Visibility.Visible; Heading.Text="Готово к работе."; Description.Text="QuickTools установлен на вашем компьютере."; StepLabel.Text="03 / ГОТОВО";
            Warnings.Text=warnings.Count==0?"Все выбранные компоненты готовы.":"Приложение установлено. Для некоторых компонентов нужно повторить установку в настройках:\n"+string.Join("\n",warnings.Select(w=>w.Length>170?w[..170]+"…":w));
        }
        catch(Exception ex)
        {
            working=false; PrimaryButton.IsEnabled=true; PrimaryButton.Content="Повторить"; Description.Text="Не удалось завершить установку."; Details.Text=ex.Message;
        }
    }
    private async Task Remove()
    {
        working=true; PrimaryButton.IsEnabled=false;
        try { await InstallerEngine.Uninstall(uninstallRoot!); finished=true; Heading.Text="QuickTools удалён."; DoneDescription.Text="Настройки и конвертеры сохранены."; PrimaryButton.Content="Закрыть"; }
        catch(Exception ex) { DoneDescription.Text=ex.Message; }
        finally { working=false; PrimaryButton.IsEnabled=true; }
    }
}


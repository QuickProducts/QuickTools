using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
namespace QuickTools;
internal static class InstallerEngine
{
    internal const string Product="QuickTools.Radial.0B6C0833";
    private const string RegistryPath=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\QuickTools";
    internal sealed record Receipt(string ProductId,string ActiveVersion,string[] Versions);
    internal static string ValidateRoot(string path)
    {
        string root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if(root.Length<6 || root==Path.GetPathRoot(root)) throw new IOException("Выберите отдельную папку для QuickTools.");
        string marker=Path.Combine(root,"quicktools-install.json");
        if(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
        {
            if(!File.Exists(marker) || JsonSerializer.Deserialize<Receipt>(File.ReadAllText(marker))?.ProductId!=Product) throw new IOException("Папка содержит другие файлы. Выберите пустую папку или существующую установку QuickTools.");
        }
        return root;
    }
    internal static async Task<string> Extract(string root,Action<double,string> progress,CancellationToken token)
    {
        root=ValidateRoot(root); Directory.CreateDirectory(root);
        string staging=Path.Combine(root,".install-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
        try
        {
            var assembly=Assembly.GetExecutingAssembly();
            using(var payload=assembly.GetManifestResourceStream("QuickTools.Payload")??throw new IOException("В установщике нет приложения"))
            using(var hashReader=new StreamReader(assembly.GetManifestResourceStream("QuickTools.PayloadHash")!))
            {
                progress(1,"Проверяем целостность приложения…");
                string hash=Convert.ToHexString(await SHA256.HashDataAsync(payload,token));
                if(!hash.Equals((await hashReader.ReadToEndAsync(token)).Trim(),StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Встроенный пакет повреждён.");
                payload.Position=0;
                using var zip=new ZipArchive(payload,ZipArchiveMode.Read);
                long total=zip.Entries.Sum(e=>e.Length),done=0;
                foreach(var entry in zip.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    string path=Path.GetFullPath(Path.Combine(staging,entry.FullName));
                    if(!path.StartsWith(staging+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Некорректный путь в пакете");
                    if(string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(path); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await using(var input=entry.Open())
                    await using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true)) await input.CopyToAsync(output,token);
                    done+=entry.Length; progress(total==0?90:done*95.0/total,"Устанавливаем QuickTools…");
                }
            }
            if(!File.Exists(Path.Combine(staging,"QuickTools.exe")) || !File.Exists(Path.Combine(staging,"coreclr.dll"))) throw new IOException("В пакете нет автономного приложения");
            string version="0.4.2-"+Guid.NewGuid().ToString("N")[..8];
            string destination=Path.Combine(root,"versions",version); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(staging,destination); progress(100,"Приложение установлено"); return destination;
        }
        finally { if(Directory.Exists(staging)) Directory.Delete(staging,true); }
    }
    internal static void Register(string root,string active,bool desktop,bool startMenu)
    {
        string marker=Path.Combine(root,"quicktools-install.json");
        var previous=File.Exists(marker)?JsonSerializer.Deserialize<Receipt>(File.ReadAllText(marker)):null;
        string relative=Path.GetRelativePath(root,active);
        var receipt=new Receipt(Product,relative,(previous?.Versions??Array.Empty<string>()).Append(relative).Distinct().ToArray());
        File.WriteAllText(marker,JsonSerializer.Serialize(receipt));
        string source=Environment.ProcessPath!,setup=Path.Combine(root,"QuickToolsSetup.exe");
        if(!source.Equals(setup,StringComparison.OrdinalIgnoreCase)) File.Copy(source,setup,true);
        string exe=Path.Combine(active,"QuickTools.exe");
        if(desktop) Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"QuickTools.lnk"),exe);
        if(startMenu) Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"QuickTools.lnk"),exe);
        using var key=Registry.CurrentUser.CreateSubKey(RegistryPath);
        key.SetValue("DisplayName","QuickTools"); key.SetValue("DisplayVersion","0.4.2"); key.SetValue("Publisher","QuickTools"); key.SetValue("InstallLocation",root); key.SetValue("DisplayIcon",exe);
        key.SetValue("UninstallString","\""+setup+"\" --uninstall \""+root+"\""); key.SetValue("NoModify",1); key.SetValue("NoRepair",1);
    }
    private static void Shortcut(string path,string exe)
    {
        dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link=shell.CreateShortcut(path);
        try { link.TargetPath=exe; link.WorkingDirectory=Path.GetDirectoryName(exe); link.Description="QuickTools — быстрые действия рядом с курсором"; link.IconLocation=exe+",0"; link.Save(); }
        finally { System.Runtime.InteropServices.Marshal.ReleaseComObject(link); System.Runtime.InteropServices.Marshal.ReleaseComObject(shell); }
    }
    internal static async Task Uninstall(string root)
    {
        string marker=Path.Combine(root,"quicktools-install.json");
        var receipt=JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(marker));
        if(receipt?.ProductId!=Product) throw new IOException("Папка не принадлежит QuickTools");
        bool running=false;
        foreach(var process in Process.GetProcessesByName("QuickTools"))
        {
            try { if(process.MainModule?.FileName?.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)==true) running=true; }
            catch(System.ComponentModel.Win32Exception) { }
            finally { process.Dispose(); }
        }
        if(running && EventWaitHandle.TryOpenExisting("Local\\QuickTools.Exit",out var signal)) { signal.Set(); signal.Dispose(); await Task.Delay(1200); }
        foreach(string relative in receipt.Versions)
        {
            string path=Path.GetFullPath(Path.Combine(root,relative));
            string versions=Path.Combine(root,"versions")+Path.DirectorySeparatorChar;
            if(!path.StartsWith(versions,StringComparison.OrdinalIgnoreCase)) throw new IOException("Неверный путь версии");
            if(Directory.Exists(path)) Directory.Delete(path,true);
        }
        string setup=Path.Combine(root,"QuickToolsSetup.exe"); if(File.Exists(setup)) File.Delete(setup);
        foreach(string shortcut in new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"QuickTools.lnk"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"QuickTools.lnk")})
            if(File.Exists(shortcut)) File.Delete(shortcut);
        Registry.CurrentUser.DeleteSubKeyTree(RegistryPath,false); File.Delete(marker);
        string versionRoot=Path.Combine(root,"versions"); if(Directory.Exists(versionRoot) && !Directory.EnumerateFileSystemEntries(versionRoot).Any()) Directory.Delete(versionRoot);
        if(!Directory.EnumerateFileSystemEntries(root).Any()) Directory.Delete(root);
    }
    internal static async Task<int> SelfTest(string report)
    {
        string root=Path.Combine(Path.GetTempPath(),"QuickToolsSetup-test-"+Guid.NewGuid().ToString("N"));
        try
        {
            string active=await Extract(root,(_,_)=>{},CancellationToken.None);
            if(!File.Exists(Path.Combine(active,"QuickTools.exe")) || !File.Exists(Path.Combine(active,"coreclr.dll"))) throw new Exception("Payload missing");
            bool rejected=false; try { ValidateRoot(active); } catch(IOException) { rejected=true; }
            if(!rejected) throw new Exception("Occupied folder accepted");
            await File.WriteAllTextAsync(report,"PASS: embedded payload SHA-256, complete offline extraction, bundled .NET runtime, occupied folder guard. No shortcuts, registry writes or dependency installations in this test.\n"); return 0;
        }
        catch(Exception ex) { await File.WriteAllTextAsync(report,"FAIL: "+ex); return 1; }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }
}




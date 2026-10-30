using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace QuickTools;
internal static class DependencyManager
{
    internal static readonly string[] Tools={"ffmpeg","magick","pandoc","typst","pdftotext"};
    private static readonly SemaphoreSlim installation=new(1);
    private static readonly HttpClient http=new() { Timeout=Timeout.InfiniteTimeSpan };
    internal static event Action<string,string>? StatusChanged;
    internal static event Action<string,double,string>? ProgressChanged;
    internal static string Root=>Path.Combine(Path.GetDirectoryName(UserSettings.FilePath)!,"tools");
    private sealed record PackageInfo(string Name,string Version,string Url,string Hash,string Format,string Executable);
    private static PackageInfo Info(string tool)=>tool switch
    {
        "ffmpeg"=>new("FFmpeg","9.0.2","https://github.com/GyanD/codexffmpeg/releases/download/9.0.2/ffmpeg-9.0.2-full_build.zip","759d0a9831c436a0eb331ad36f236c06cb04aaa0005da46571f0e9d3d9206f6b","zip","ffmpeg.exe"),
        "magick"=>new("ImageMagick","7.1.2-34","https://github.com/ImageMagick/ImageMagick/releases/download/7.1.2-34/ImageMagick-7.1.2-34-portable-Q16-HDRI-x64.7z","8dcfd3644b7fc20a92aeedeb4798024e5947f25f4d7c0c313c883fbca3d723e6","7z","magick.exe"),
        "pandoc"=>new("Pandoc","3.13","https://github.com/jgm/pandoc/releases/download/3.13/pandoc-3.13-windows-x86_64.zip","23e1f2e978a3df80db8bf65c9c59534f41a23140480cfb4f783a882ab7dc6a4e","zip","pandoc.exe"),
        "typst"=>new("Typst","0.15.1","https://github.com/typst/typst/releases/download/v0.15.1/typst-x86_64-pc-windows-msvc.zip","19ce3551153c2fe7ee9fa2f95208310c8f4d3209fedb699e0333faf8913f6736","zip","typst.exe"),
        "pdftotext" or "pdftoppm"=>new("Poppler","26.09.0","https://github.com/oschwartz10612/poppler-windows/releases/download/v26.09.0-0/Release-26.09.0-0.zip","7a6f256a0ddf7536182246a5733331bf4677cbcc34f4663774947ad34556c8d0","zip",tool+".exe"),
        _=>throw new ArgumentException("Неизвестный конвертер: "+tool)
    };
    internal static string Package(string tool)=>Info(tool).Name;
    internal static async Task<string> Ensure(string tool,Action<string> progress,CancellationToken token,bool explicitInstall=false)
    {
        var found=ToolLocator.Find(tool); if(found!=null) return found;
        string configKey=tool=="pdftoppm"?"pdftotext":tool;
        if(UserSettings.Current.ToolPaths.TryGetValue(configKey,out var configured) && !string.IsNullOrWhiteSpace(configured)) throw new FileNotFoundException("Проверьте пользовательский путь к "+tool+" в настройках.");
        if(!explicitInstall && !UserSettings.Current.AutoInstallTools) throw new FileNotFoundException("Включите автоматическую установку конвертеров в настройках.");
        await installation.WaitAsync(token);
        string? staging=null;
        try
        {
            found=ToolLocator.Find(tool); if(found!=null) return found;
            var package=Info(tool);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(30));
            var ct=timeout.Token;
            void Report(double percent,string message) { progress(message); StatusChanged?.Invoke(tool,message); ProgressChanged?.Invoke(tool,percent,message); }
            Directory.CreateDirectory(Root);
            string cache=Path.Combine(Root,"downloads"); Directory.CreateDirectory(cache);
            string archive=Path.Combine(cache,package.Name+"-"+package.Version+"."+package.Format);
            bool valid=File.Exists(archive) && await CheckHash(archive,package.Hash,ct);
            if(!valid)
            {
                string partial=archive+".partial";
                try
                {
                    using var request=new HttpRequestMessage(HttpMethod.Get,package.Url); request.Headers.UserAgent.ParseAdd("QuickTools/0.4");
                    using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct); response.EnsureSuccessStatusCode();
                    long? total=response.Content.Headers.ContentLength;
                    await using(var source=await response.Content.ReadAsStreamAsync(ct))
                    await using(var output=new FileStream(partial,FileMode.Create,FileAccess.Write,FileShare.None,81920,true))
                    {
                        byte[] buffer=new byte[81920]; long received=0; int length; var clock=Stopwatch.StartNew();
                        while((length=await source.ReadAsync(buffer,ct))>0)
                        {
                            await output.WriteAsync(buffer.AsMemory(0,length),ct); received+=length;
                            if(clock.ElapsedMilliseconds>200) { Report(total>0?received*75.0/total.Value:0,$"{package.Name}: скачано {received/1048576.0:0.0} МБ"+(total>0?$" / {total.Value/1048576.0:0.0} МБ":"")); clock.Restart(); }
                        }
                    }
                    Report(77,package.Name+": проверка SHA-256…");
                    if(!await CheckHash(partial,package.Hash,ct)) throw new InvalidDataException("Контрольная сумма "+package.Name+" не совпала. Файл не будет запущен.");
                    File.Move(partial,archive,true);
                }
                finally { if(File.Exists(partial)) File.Delete(partial); }
            }
            Report(82,package.Name+": установка…");
            {
                staging=Path.Combine(Root,".stage-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
                if(package.Format=="zip") await Task.Run(()=>ZipFile.ExtractToDirectory(archive,staging),ct);
                else
                {
                    string tar=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"tar.exe");
                    var info=new ProcessStartInfo(tar) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true };
                    foreach(string arg in new[]{"-xf",archive,"-C",staging}) info.ArgumentList.Add(arg);
                    using var process=Process.Start(info)??throw new IOException("Не удалось распаковать "+package.Name);
                    var error=process.StandardError.ReadToEndAsync();
                    using var cancel=ct.Register(()=> { try { if(!process.HasExited) process.Kill(true); } catch(InvalidOperationException) { } });
                    await process.WaitForExitAsync(CancellationToken.None); string detail=await error; ct.ThrowIfCancellationRequested();
                    if(process.ExitCode!=0) throw new IOException("Распаковка "+package.Name+": "+detail);
                }
                ct.ThrowIfCancellationRequested();
                string exe=Directory.EnumerateFiles(staging,package.Executable,SearchOption.AllDirectories).FirstOrDefault()??throw new IOException("В пакете нет "+package.Executable);
                string relative=Path.GetRelativePath(staging,exe);
                string destination=Path.Combine(Root,package.Name+"-"+package.Version+"-"+Guid.NewGuid().ToString("N")[..8]);
                Directory.Move(staging,destination); staging=null;
                // Locator scans managed folders, so setup does not overwrite user's custom paths.
                found=Path.Combine(destination,relative);
            }
            found??=ToolLocator.Find(tool);
            if(found==null) throw new IOException(package.Name+" установлена, но "+tool+" не найден. Укажите EXE в настройках.");
            WriteLog(package.Name+": "+found); Report(100,package.Name+": готово"); return found;
        }
        catch(Exception ex) { WriteLog(ex.ToString()); StatusChanged?.Invoke(tool,"Ошибка: "+ex.Message); throw; }
        finally { if(staging!=null && Directory.Exists(staging)) Directory.Delete(staging,true); installation.Release(); }
    }
    internal static async Task<bool> InstallAll(Action<string> progress,CancellationToken token)
    {
        bool success=true;
        foreach(string tool in Tools)
        {
            token.ThrowIfCancellationRequested();
            try { await Ensure(tool,progress,token,true); }
            catch(OperationCanceledException) { throw; }
            catch(Exception ex) { success=false; progress(ex.Message); }
        }
        return success;
    }
    internal static async Task<bool> CheckHash(string path,string expected,CancellationToken token)
    { await using var stream=File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream,token)).Equals(expected,StringComparison.OrdinalIgnoreCase); }
    internal static string LogPath=>Path.Combine(Path.GetDirectoryName(UserSettings.FilePath)!,"setup.log");
    private static void WriteLog(string message)
    { try { Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!); File.AppendAllText(LogPath,DateTime.Now.ToString("s")+" "+message+Environment.NewLine); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
}

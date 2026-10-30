using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace QuickTools;
internal sealed record ShellReply(string[] Paths,string? Error=null);
internal sealed class ShellQueryService : IDisposable
{
    internal const int TimeoutMilliseconds=1800;
    private readonly CancellationTokenSource shutdown=new();
    private int busy;
    internal static bool IsShellTarget(Native.POINT point,bool selection)
    {
        var foreground=Native.GetForegroundWindow();
        var root=selection?foreground:Native.GetAncestor(Native.WindowFromPoint(point),2);
        string type=Native.Class(root);
        return (type is "CabinetWClass" or "ExploreWClass" && root==foreground) || type is "Progman" or "WorkerW";
    }
    internal async Task<ShellReply> Resolve(Native.POINT point,bool selection,CancellationToken cancel)
    {
        if(Interlocked.CompareExchange(ref busy,1,0)!=0) return new(Array.Empty<string>());
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel,shutdown.Token); timeout.CancelAfter(TimeoutMilliseconds);
            var start=WorkerStart(point,selection);
            using var process=Process.Start(start)??throw new IOException("Не удалось запустить определение файла");
            using var registration=timeout.Token.Register(()=> { try { if(!process.HasExited) process.Kill(true); } catch(InvalidOperationException) { } catch(System.ComponentModel.Win32Exception) { } });
            var output=process.StandardOutput.ReadToEndAsync(); var errors=process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(CancellationToken.None); string json=await output; string error=await errors;
            if(timeout.IsCancellationRequested)
            {
                cancel.ThrowIfCancellationRequested(); shutdown.Token.ThrowIfCancellationRequested();
                return new(Array.Empty<string>(),"Проводник не ответил вовремя. Попробуйте Ctrl + Space.");
            }
            if(process.ExitCode!=0) return new(Array.Empty<string>(),"Определение файла завершилось с ошибкой "+process.ExitCode);
            return JsonSerializer.Deserialize<ShellReply>(json)??new(Array.Empty<string>(),"Нет ответа Проводника");
        }
        finally { Volatile.Write(ref busy,0); }
    }
    internal static ProcessStartInfo WorkerStart(Native.POINT point,bool selection)
    {
        string executable=Environment.ProcessPath!;
        var start=new ProcessStartInfo(executable) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        if(Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(typeof(ShellQueryService).Assembly.Location);
        foreach(string arg in new[]{"--resolve-shell",point.X.ToString(System.Globalization.CultureInfo.InvariantCulture),point.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),selection?"selection":"hover"}) start.ArgumentList.Add(arg);
        return start;
    }
    public void Dispose()=>shutdown.Cancel();
}

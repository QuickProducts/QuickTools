using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace QuickTools;
internal sealed record ProcessOutput(int ExitCode,string Output,string Error);
internal static class ProcessRunner
{
    internal static async Task<ProcessOutput> Run(string executable,string[] arguments,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var info=new ProcessStartInfo(executable) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        foreach(string argument in arguments) info.ArgumentList.Add(argument);
        using var process=Process.Start(info)??throw new IOException("Не удалось запустить "+Path.GetFileName(executable));
        try { process.PriorityClass=ProcessPriorityClass.BelowNormal; } catch(System.ComponentModel.Win32Exception) { } catch(InvalidOperationException) { }
        using var registration=token.Register(()=> { try { if(!process.HasExited) process.Kill(true); } catch(InvalidOperationException) { } catch(System.ComponentModel.Win32Exception) { } });
        var output=Drain(process.StandardOutput,1048576); var error=Drain(process.StandardError,16384);
        await process.WaitForExitAsync(CancellationToken.None);
        var result=new ProcessOutput(process.ExitCode,await output,await error); token.ThrowIfCancellationRequested(); return result;
    }
    private static async Task<string> Drain(StreamReader reader,int limit)
    {
        var text=new StringBuilder(); char[] buffer=new char[4096]; int length;
        while((length=await reader.ReadAsync(buffer,0,buffer.Length))>0) { text.Append(buffer,0,length); if(text.Length>limit) text.Remove(0,text.Length-limit); }
        return text.ToString();
    }
}

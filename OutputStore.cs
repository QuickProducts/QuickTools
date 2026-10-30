using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace QuickTools;
internal sealed record SavedOutput(string Path,bool UsedFallback);
internal static class OutputStore
{
    internal static string FallbackDirectory=>System.IO.Path.Combine(System.IO.Path.GetDirectoryName(UserSettings.FilePath)!,"Converted");
    internal static async Task<SavedOutput> Commit(string temporary,string source,string extension,CancellationToken token,string? preferredDirectory=null)
    {
        string preferred=preferredDirectory??System.IO.Path.GetDirectoryName(source)!;
        try { return new(await Place(temporary,source,extension,preferred,token),false); }
        catch(Exception ex) when(ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            RuntimeLog.Write("Output folder unavailable: "+preferred,ex);
            return new(await Place(temporary,source,extension,FallbackDirectory,token),true);
        }
    }
    private static async Task<string> Place(string temporary,string source,string extension,string folder,CancellationToken token)
    {
        Directory.CreateDirectory(folder);
        string stage=System.IO.Path.Combine(folder,".quicktools-"+Guid.NewGuid().ToString("N")+".partial");
        try
        {
            await using(var input=File.OpenRead(temporary))
            await using(var output=new FileStream(stage,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true)) await input.CopyToAsync(output,token);
            string name=System.IO.Path.GetFileNameWithoutExtension(source); if(name.Length>120) name=name[..120];
            for(int i=0;i<10000;i++)
            {
                token.ThrowIfCancellationRequested();
                string final=System.IO.Path.Combine(folder,name+"_converted"+(i==0?"":"_"+i)+"."+extension);
                try { File.Move(stage,final,false); return final; }
                catch(IOException) when(File.Exists(final) || Directory.Exists(final)) { }
            }
            throw new IOException("Не удалось подобрать имя результата.");
        }
        finally { try { if(File.Exists(stage)) File.Delete(stage); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
    }
}

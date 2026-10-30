using System;
using System.IO;
namespace QuickTools;
internal static class RuntimeLog
{
    internal static bool Enabled { get; set; }=true;
    internal static void Write(string context,Exception error)
    {
        if(!Enabled) return;
        try { string folder=Path.GetDirectoryName(UserSettings.FilePath)!; Directory.CreateDirectory(folder); string path=Path.Combine(folder,"runtime.log"); if(File.Exists(path) && new FileInfo(path).Length>524288) File.WriteAllText(path,""); File.AppendAllText(path,DateTime.Now.ToString("s")+" "+context+Environment.NewLine+error+Environment.NewLine); }
        catch(IOException) { } catch(UnauthorizedAccessException) { }
    }
}

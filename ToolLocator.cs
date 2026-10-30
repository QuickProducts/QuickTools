using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace QuickTools;
internal static class ToolLocator
{
    private static readonly ConcurrentDictionary<string,string> cache=new();
    internal static string? Find(string name)
    {
        if(name=="ffprobe")
        {
            string? ffmpeg=Find("ffmpeg");
            if(ffmpeg!=null) { string sibling=Path.Combine(Path.GetDirectoryName(ffmpeg)!,"ffprobe.exe"); if(File.Exists(sibling)) return sibling; }
        }
        if(UserSettings.Current.ToolPaths.TryGetValue(name,out var configured) && !string.IsNullOrWhiteSpace(configured)) return File.Exists(configured)?configured:null;
        // Both Poppler tools live side by side; a custom pdftotext path also configures rendering.
        if(name=="pdftoppm" && UserSettings.Current.ToolPaths.TryGetValue("pdftotext",out var pdf) && !string.IsNullOrWhiteSpace(pdf))
        { string sibling=Path.Combine(Path.GetDirectoryName(pdf)??"",name+".exe"); return File.Exists(sibling)?sibling:null; }
        if(cache.TryGetValue(name,out var cached) && File.Exists(cached)) return cached;
        foreach(string path in Candidates(name)) if(File.Exists(path)) { cache[name]=path; return path; }
        return null;
    }
    private static IEnumerable<string> Candidates(string name)
    {
        yield return Path.Combine(AppContext.BaseDirectory,"tools",name+".exe");
        string managed=Path.Combine(Path.GetDirectoryName(UserSettings.FilePath)!,"tools");
        string family=name switch { "ffmpeg" or "ffprobe"=>"FFmpeg-","magick"=>"ImageMagick-","pandoc"=>"Pandoc-","typst"=>"Typst-",_=>"Poppler-" };
        foreach(string folder in Directories(managed,family+"*")) foreach(string path in BoundedSearch(folder,name+".exe",5)) yield return path;
        string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string paths=string.Join(Path.PathSeparator,Environment.GetEnvironmentVariable("PATH"),Environment.GetEnvironmentVariable("PATH",EnvironmentVariableTarget.User),Environment.GetEnvironmentVariable("PATH",EnvironmentVariableTarget.Machine));
        foreach(string folder in paths.Split(Path.PathSeparator).Where(p=>!string.IsNullOrWhiteSpace(p))) yield return Path.Combine(Environment.ExpandEnvironmentVariables(folder.Trim('"')),name+".exe");
        yield return Path.Combine(local,"Microsoft","WinGet","Links",name+".exe");
        foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Path.Combine(local,"Programs")})
        {
            if(string.IsNullOrEmpty(root)) continue;
            if(name=="pandoc") yield return Path.Combine(root,"Pandoc","pandoc.exe");
            if(name=="magick") foreach(string dir in Directories(root,"ImageMagick*")) yield return Path.Combine(dir,"magick.exe");
        }
        string prefix=name switch { "ffmpeg"=>"Gyan.FFmpeg_","magick"=>"ImageMagick.ImageMagick","pandoc"=>"JohnMacFarlane.Pandoc_","typst"=>"Typst.Typst_",_=>"oschwartz10612.Poppler_" };
        foreach(string root in new[]{Path.Combine(local,"Microsoft","WinGet","Packages"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WinGet","Packages")})
            foreach(string folder in Directories(root,prefix+"*"))
                foreach(string path in BoundedSearch(folder,name+".exe",5)) yield return path;
    }
    private static string[] Directories(string root,string pattern)
    { try { return Directory.Exists(root)?Directory.GetDirectories(root,pattern):Array.Empty<string>(); } catch(IOException) { return Array.Empty<string>(); } catch(UnauthorizedAccessException) { return Array.Empty<string>(); } }
    private static IEnumerable<string> BoundedSearch(string root,string filename,int depth)
    {
        yield return Path.Combine(root,filename); if(depth<=0) yield break;
        foreach(string dir in Directories(root,"*"))
        {
            FileAttributes attributes; try { attributes=File.GetAttributes(dir); } catch(IOException) { continue; } catch(UnauthorizedAccessException) { continue; }
            if((attributes&FileAttributes.ReparsePoint)!=0) continue;
            foreach(string file in BoundedSearch(dir,filename,depth-1)) yield return file;
        }
    }
}

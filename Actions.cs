using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
namespace QuickTools;
internal record ToolAction(string Label,string Extension,string Engine,string[] Arguments);
internal static class Actions
{
    internal static string Id(ToolAction action)=>action.Engine+":"+action.Extension;
    internal static ToolAction[] VisibleFor(string[] files)=>For(files).Where(a=>!UserSettings.Current.HiddenActions.Contains(Id(a))).ToArray();
    internal static string Kind(string path)
    {
        string e=Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        if(Has("mp4 mkv mov avi webm m4v wmv mpeg mpg ts flv mts m2ts 3gp ogv",e)) return "video";
        if(Has("mp3 wav flac ogg aac m4a opus aiff aif wma ac3 amr mka",e)) return "audio";
        if(Has("png jpg jpeg webp bmp tif tiff gif ico avif heic heif jxl tga ppm pgm pbm svg psd dds exr",e)) return "image";
        if(Has("docx odt rtf epub fb2 org rst tex",e)) return "document";
        if(Has("xlsx xls ods xlsm xlsb",e)) return "spreadsheet";
        if(Has("pptx ppt odp ppsx",e)) return "presentation";
        if(e=="pdf") return "pdf";
        if(Has("txt md markdown csv tsv json xml yaml yml log ini cfg conf toml cs py js ts html htm css sql bat cmd ps1 sh c cpp h hpp java go rs rb php lua vbs reg srt ass vtt",e)) return "text";
        return "other";
    }
    private static bool Has(string list,string extension)=>list.Split(' ').Contains(extension);
    internal static string Mime(string path)
    {
        using var key=Registry.ClassesRoot.OpenSubKey(Path.GetExtension(path));
        return key?.GetValue("Content Type") as string??"application/octet-stream";
    }
    private static ToolAction Media(string name,string ext,params string[] args)=>new(name,ext,"ffmpeg",args);
    private static ToolAction Document(string ext)=>new(ext.ToUpperInvariant(),ext,ext=="pdf"?"pandocpdf":"pandoc",Array.Empty<string>());
    internal static ToolAction[] For(string[] files)
    {
        if(files.Length==0) return Array.Empty<ToolAction>();
        var kinds=files.Select(Kind).Distinct().ToArray(); var list=new List<ToolAction>();
        if(kinds.Length==1) switch(kinds[0])
        {
            case "video":
                list.AddRange(new[]{
                    Media("MP4","mp4","-c:v","libx264","-crf","23","-c:a","aac","-movflags","+faststart"),
                    Media("WebM","webm","-c:v","libvpx-vp9","-crf","28","-b:v","0","-c:a","libopus","-b:a","160k"),
                    Media("MKV","mkv","-c:v","libx264","-crf","23","-c:a","aac"),
                    Media("MOV","mov","-c:v","libx264","-c:a","aac"),
                    Media("AVI","avi","-c:v","mpeg4","-q:v","5","-c:a","libmp3lame"),
                    Media("GIF","gif","-filter_complex","[0:v]fps=12,scale=480:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse[gifout]","-an"),
                    Media("MP3","mp3","-vn","-c:a","libmp3lame","-q:a","2"),
                    Media("WAV","wav","-vn","-c:a","pcm_s16le"),
                    Media("FLAC","flac","-vn","-c:a","flac"),
                    Media("AAC","m4a","-vn","-c:a","aac","-b:a","192k"),
                    Media("MPEG","mpg","-c:v","mpeg2video","-q:v","4","-c:a","mp2"),
                    Media("TS","ts","-c:v","libx264","-c:a","aac"),
                    Media("Кадр PNG","png","-frames:v","1","-update","1")}); break;
            case "audio":
                list.AddRange(new[]{Media("MP3","mp3","-vn","-c:a","libmp3lame","-q:a","2"),Media("WAV","wav","-vn","-c:a","pcm_s16le"),Media("AAC","m4a","-vn","-c:a","aac","-b:a","192k"),Media("FLAC","flac","-vn","-c:a","flac"),Media("Opus","opus","-vn","-c:a","libopus","-b:a","128k"),Media("OGG","ogg","-vn","-c:a","libvorbis","-q:a","5"),Media("AIFF","aiff","-vn","-c:a","pcm_s16be"),Media("WMA","wma","-vn","-c:a","wmav2","-b:a","192k"),Media("AAC raw","aac","-vn","-c:a","aac","-b:a","192k"),Media("AC3","ac3","-vn","-c:a","ac3","-b:a","192k")}); break;
            case "image":
                foreach(string ext in new[]{"png","jpg","webp","ico","pdf","avif","bmp","tiff","gif","tga","ppm"})
                    list.Add(new(ext.ToUpperInvariant(),ext,"magick",ext=="ico"?new[]{"-resize","256x256","-background","none","-gravity","center","-extent","256x256","-define","icon:auto-resize=256,128,64,48,32,16"}:ext is "jpg" or "webp" or "avif"?new[]{"-quality","90"}:Array.Empty<string>())); break;
            case "document": foreach(string ext in new[]{"pdf","docx","odt","rtf","txt","md","html","epub","tex","org","rst","pptx"}) list.Add(Document(ext)); break;
            case "pdf":
                list.Add(new("Текст","txt","pdftotext",Array.Empty<string>()));
                list.Add(new("PNG · стр. 1","png","pdfimage",new[]{"-png"}));
                list.Add(new("JPG · стр. 1","jpg","pdfimage",new[]{"-jpeg"})); break;
            case "text":
                foreach(string ext in new[]{"bat","cmd","ps1","txt","md","py","js","ts","html","css","json","xml","yaml","toml","ini","cfg","csv","tsv","sql","sh","vbs","reg","log","srt","vtt","cs","cpp","java","go","rs","php","lua"})
                    list.Add(new(ext.ToUpperInvariant(),ext,"copytext",Array.Empty<string>()));
                list.Add(new("UTF-8","txt","text",Array.Empty<string>()));
                foreach(string ext in new[]{"pdf","docx","odt","rtf","epub"}) list.Add(Document(ext)); break;
        }
        list.Add(new("ZIP","zip","zip",Array.Empty<string>()));
        list.Add(new("TAR","tar","tar",Array.Empty<string>()));
        list.Add(new("GZIP","gz","gzip",Array.Empty<string>()));
        list.Add(new("SHA-256","sha256","hash",Array.Empty<string>()));
        list.Add(new("Пути","","clipboard",Array.Empty<string>()));
        return list.ToArray();
    }
}

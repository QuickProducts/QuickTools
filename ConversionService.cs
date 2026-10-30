using System;
using System.IO;
using System.IO.Compression;
using System.Formats.Tar;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
namespace QuickTools;
internal sealed record ConversionResult(string[] Outputs,string[] Errors,bool UsedFallback);
internal sealed class ConversionService : IDisposable
{
    private readonly SemaphoreSlim queue=new(1);
    private readonly CancellationTokenSource shutdown=new();
    internal async Task<ConversionResult> Run(string[] files,ToolAction action,Action<string> notify)
    {
        var outputs=new System.Collections.Generic.List<string>(); var errors=new System.Collections.Generic.List<string>();
        var token=shutdown.Token; bool entered=false,fallback=false;
        ConversionHistory.JobStarted();
        try
        {
            await queue.WaitAsync(token); entered=true;
            string? required=action.Engine switch { "pandocpdf"=>"pandoc","pdfimage"=>"pdftoppm","ffmpeg" or "magick" or "pdftotext" or "pandoc"=>action.Engine,_=>null };
            if(required!=null) await DependencyManager.Ensure(required,_=>{},token);
            if(action.Engine=="pandocpdf") await DependencyManager.Ensure("typst",_=>{},token);
            await Task.Run(async ()=>
            {
                foreach(string file in files)
                {
                    token.ThrowIfCancellationRequested(); string? work=null;
                    try
                    {
                        // Validate source access separately from destination access.
                        using(var source=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) { }
                        work=Path.Combine(Path.GetDirectoryName(UserSettings.FilePath)!,"work",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
                        string temporary=Path.Combine(work,"result."+action.Extension);
                        await ConvertFile(file,temporary,work,action,token);
                        if(!File.Exists(temporary) || (new FileInfo(temporary).Length==0 && action.Engine is not ("copytext" or "text" or "pdftotext"))) throw new IOException("Конвертер не создал результат.");
                        var saved=await OutputStore.Commit(temporary,file,action.Extension,token); outputs.Add(saved.Path); fallback|=saved.UsedFallback;
                        ConversionHistory.Add(new(DateTime.Now,file,action.Label,saved.Path,null,saved.UsedFallback));
                    }
                    catch(OperationCanceledException) { throw; }
                    catch(Exception ex)
                    {
                        RuntimeLog.Write($"Conversion {file} → {action.Extension} ({action.Engine})",ex);
                        string message=Friendly(ex); errors.Add(Path.GetFileName(file)+": "+message);
                        ConversionHistory.Add(new(DateTime.Now,file,action.Label,null,message));
                    }
                    finally { Cleanup(work); }
                }
            },token);
        }
        catch(OperationCanceledException) { errors.Add("Операция отменена."); }
        catch(Exception ex)
        {
            RuntimeLog.Write("Conversion preparation: "+action.Engine,ex); string message=Friendly(ex); errors.Add(message);
            foreach(string file in files) ConversionHistory.Add(new(DateTime.Now,file,action.Label,null,message));
        }
        finally { if(entered) queue.Release(); ConversionHistory.JobFinished(); }
        if(errors.Count>0) notify($"Ошибка: {errors.Count}. Готово: {outputs.Count}. Подробности в «Задачи и результаты».");
        else notify(fallback?"Готово. Папка исходника недоступна — результат в QuickTools\\Converted. Нажмите уведомление.":$"Готово: {outputs.Count} файл(ов) → {action.Label}");
        return new(outputs.ToArray(),errors.ToArray(),fallback);
    }
    private static async Task ConvertFile(string file,string output,string work,ToolAction action,CancellationToken token)
    {
        switch(action.Engine)
        {
            case "zip": using(var zip=ZipFile.Open(output,ZipArchiveMode.Create)) zip.CreateEntryFromFile(file,Path.GetFileName(file),CompressionLevel.Optimal); break;
            case "tar": using(var tar=new TarWriter(File.Create(output),TarEntryFormat.Pax,false)) tar.WriteEntry(file,Path.GetFileName(file)); break;
            case "gzip":
                await using(var source=File.OpenRead(file)) await using(var target=File.Create(output)) await using(var gzip=new GZipStream(target,CompressionLevel.Optimal)) await source.CopyToAsync(gzip,token); break;
            case "copytext": await using(var source=File.OpenRead(file)) await using(var target=File.Create(output)) await source.CopyToAsync(target,token); break;
            case "text": await File.WriteAllTextAsync(output,await File.ReadAllTextAsync(file,token),new UTF8Encoding(false),token); break;
            case "hash": await using(var source=File.OpenRead(file)) { var hash=await SHA256.HashDataAsync(source,token); await File.WriteAllTextAsync(output,Convert.ToHexString(hash).ToLowerInvariant()+"  "+Path.GetFileName(file)+Environment.NewLine,token); } break;
            case "ffmpeg": await MediaConverter.Convert(file,output,action,token); break;
            case "magick":
                var imageArgs=new System.Collections.Generic.List<string> { file+"[0]","-auto-orient" };
                imageArgs.AddRange(action.Arguments);
                if(action.Extension is "jpg" or "pdf") imageArgs.AddRange(new[]{"-background","white","-alpha","remove","-alpha","off"});
                imageArgs.Add(output); await Execute("magick",imageArgs.ToArray(),token); break;
            case "pdftotext": await Execute("pdftotext",new[]{"-layout","-enc","UTF-8",file,output},token); break;
            case "pdfimage":
                string prefix=Path.Combine(work,"render");
                await Execute("pdftoppm",new[]{"-f","1","-singlefile","-r","150"}.Concat(action.Arguments).Concat(new[]{file,prefix}).ToArray(),token);
                File.Move(prefix+"."+action.Extension,output); break;
            case "pandoc": case "pandocpdf":
                string inputFormat=Path.GetExtension(file).ToLowerInvariant() switch { ".docx"=>"docx",".odt"=>"odt",".rtf"=>"rtf",".epub"=>"epub",".html" or ".htm"=>"html",".org"=>"org",".rst"=>"rst",".tex"=>"latex",".fb2"=>"fb2",_=>"markdown" };
                string outputFormat=action.Extension switch { "txt"=>"plain","md"=>"gfm","tex"=>"latex","epub"=>"epub3","pdf"=>"typst",_=>action.Extension };
                var args=new System.Collections.Generic.List<string> { "--sandbox","--standalone","--from",inputFormat,"--to",outputFormat,"--resource-path",Path.GetDirectoryName(file)!,file,"--output",output };
                if(action.Engine=="pandocpdf") args.Add("--pdf-engine="+Locate("typst")); if(action.Extension=="html") args.Add("--embed-resources");
                await Execute("pandoc",args.ToArray(),token); break;
            default: throw new InvalidOperationException("Неизвестное действие: "+action.Engine);
        }
    }
    internal static string? Locate(string name)=>ToolLocator.Find(name);
    private static async Task Execute(string tool,string[] args,CancellationToken token)
    {
        string exe=Locate(tool)??throw new FileNotFoundException(tool+".exe не найден.");
        var result=await ProcessRunner.Run(exe,args,token);
        if(result.ExitCode!=0) throw new IOException(tool+": "+result.Error.Trim());
    }
    private static string Friendly(Exception ex)=>ex switch
    {
        UnauthorizedAccessException=>"Нет доступа к исходнику или рабочей папке. Подробности сохранены в runtime.log.",
        System.ComponentModel.Win32Exception e when e.NativeErrorCode==5=>"Windows запретила запуск конвертера. Подробности сохранены в runtime.log.",
        FileNotFoundException=>ex.Message,
        _=>ex.Message.Length>1500?ex.Message[..1500]+"…":ex.Message
    };
    private static void Cleanup(string? folder)
    {
        if(folder==null) return;
        try { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { RuntimeLog.Write("Temporary cleanup: "+folder,ex); }
    }
    public void Dispose()=>shutdown.Cancel();
}

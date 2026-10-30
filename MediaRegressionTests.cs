using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace QuickTools;
internal static class MediaRegressionTests
{
    internal static async Task<int> Run(string report)
    {
        string folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!,".build","media-fixtures"); Directory.CreateDirectory(folder);
        var lines=new List<string>(); int failed=0;
        try
        {
            var ffmpeg=ConversionService.Locate("ffmpeg")??throw new Exception("FFmpeg absent");
            string normal=Path.Combine(folder,"Видео пробел.mp4"),odd=Path.Combine(folder,"odd.mp4"),silent=Path.Combine(folder,"silent.mp4"),surround=Path.Combine(folder,"surround.mp4");
            await Command(ffmpeg,new[]{"-y","-v","error","-f","lavfi","-i","testsrc2=size=320x180:rate=24:duration=2","-f","lavfi","-i","sine=frequency=880:sample_rate=44100:duration=2","-c:v","libx264","-preset","ultrafast","-pix_fmt","yuv420p","-c:a","aac","-shortest",normal});
            await Command(ffmpeg,new[]{"-y","-v","error","-f","lavfi","-i","testsrc=size=321x181:rate=24:duration=2","-c:v","libx264","-preset","ultrafast","-pix_fmt","yuv444p",odd});
            await Command(ffmpeg,new[]{"-y","-v","error","-f","lavfi","-i","testsrc2=size=320x180:rate=24:duration=2","-c:v","libx264","-preset","ultrafast",silent});
            await Command(ffmpeg,new[]{"-y","-v","error","-f","lavfi","-i","testsrc2=size=320x180:rate=24:duration=2","-f","lavfi","-i","anullsrc=channel_layout=5.1:sample_rate=96000","-t","2","-c:v","libx264","-preset","ultrafast","-c:a","aac","-shortest",surround});
            using var service=new ConversionService();
            foreach(var (input,extensions) in new[]{(normal,new[]{"webm","mp4","mkv","mov","avi","gif","mp3","wav","flac","m4a","mpg","ts","png"}),(odd,new[]{"webm","mp4"}),(silent,new[]{"webm"}),(surround,new[]{"webm"})})
            {
                foreach(string extension in extensions)
                {
                    string prefix=Path.GetFileNameWithoutExtension(input)+"_converted";
                    foreach(string previous in Directory.GetFiles(folder,prefix+"*."+extension)) File.Delete(previous);
                    string status=""; var action=Actions.For(new[]{input}).Single(a=>a.Engine=="ffmpeg" && a.Extension==extension);
                    var clock=Stopwatch.StartNew(); var conversion=await service.Run(new[]{input},action,s=>status=s);
                    string output=Directory.GetFiles(folder,prefix+"*."+extension).SingleOrDefault()??"";
                    try
                    {
                        if(!File.Exists(output)) throw new Exception(string.Join("; ",conversion.Errors)+" "+status);
                        using var info=await Probe(output);
                        string format=info.RootElement.GetProperty("format").GetProperty("format_name").GetString()??"";
                        var streams=info.RootElement.GetProperty("streams").EnumerateArray().ToArray();
                        var codecs=string.Join(",",streams.Select(s=>s.GetProperty("codec_name").GetString()));
                        if(extension=="webm" && (!format.Contains("webm") || !streams.Any(s=>Get(s,"codec_type")=="video" && Get(s,"codec_name")=="vp9"))) throw new Exception("Incorrect WebM container/video codec");
                        if(extension=="webm" && input!=silent && input!=odd && !streams.Any(s=>Get(s,"codec_type")=="audio" && Get(s,"codec_name")=="opus")) throw new Exception("Audio lost");
                        if(extension is "mp4" or "webm")
                        {
                            var video=streams.First(s=>Get(s,"codec_type")=="video");
                            if(Get(video,"pix_fmt")!="yuv420p") throw new Exception("Incompatible pixel format: "+Get(video,"pix_fmt"));
                            if(video.GetProperty("width").GetInt32()%2!=0 || video.GetProperty("height").GetInt32()%2!=0) throw new Exception("Odd output dimensions");
                        }
                        if(extension=="webm" && input==normal)
                        {
                            using var original=await Probe(input);
                            double before=double.Parse(Get(original.RootElement.GetProperty("format"),"duration"),System.Globalization.CultureInfo.InvariantCulture);
                            double after=double.Parse(Get(info.RootElement.GetProperty("format"),"duration"),System.Globalization.CultureInfo.InvariantCulture);
                            if(Math.Abs(before-after)>.15) throw new Exception("WebM duration changed");
                            var video=streams.First(s=>Get(s,"codec_type")=="video");
                            if(video.GetProperty("width").GetInt32()!=320 || video.GetProperty("height").GetInt32()!=180) throw new Exception("WebM dimensions changed");
                        }
                        // Decode the actual output, not just its extension or existence.
                        await Command(ffmpeg,new[]{"-v","error","-i",output,"-f","null","-"});
                        lines.Add($"PASS {Path.GetFileName(input)} → {extension}: {format}; {codecs}; {clock.ElapsedMilliseconds} ms");
                    }
                    catch(Exception ex) { failed++; lines.Add($"FAIL {Path.GetFileName(input)} → {extension}: {ex.Message}"); }
                }
            }
            string completed=Path.Combine(folder,"Видео пробел_converted.webm");
            string blocked=Path.Combine(folder,"blocked-destination"); await File.WriteAllTextAsync(blocked,"This is a file, not a writable output folder.");
            string? fallbackOutput=null;
            try
            {
                var saved=await OutputStore.Commit(completed,normal,"webm",CancellationToken.None,blocked); fallbackOutput=saved.Path;
                if(!saved.UsedFallback || !File.Exists(saved.Path) || !(await File.ReadAllBytesAsync(saved.Path)).SequenceEqual(await File.ReadAllBytesAsync(completed))) throw new Exception("Fallback lost data");
                lines.Add("PASS inaccessible destination: intact result saved to fallback, no empty final placeholder");
            }
            catch(Exception ex) { failed++; lines.Add("FAIL inaccessible destination: "+ex.Message); }
            finally { if(fallbackOutput!=null && File.Exists(fallbackOutput)) File.Delete(fallbackOutput); }
            var noAudio=await service.Run(new[]{silent},Actions.For(new[]{silent}).Single(a=>a.Engine=="ffmpeg" && a.Extension=="mp3"),_=>{});
            if(noAudio.Outputs.Length!=0 || !noAudio.Errors.Any(e=>e.Contains("нет звуковой"))) { failed++; lines.Add("FAIL missing audio should produce a clear error, not an empty MP3"); }
            else lines.Add("PASS missing audio: clear failure, no bogus MP3 result");
        }
        catch(Exception ex) { failed++; lines.Add("FAIL: "+ex); }
        lines.Add($"Total failures: {failed}. Fixtures retained in {folder}"); await File.WriteAllLinesAsync(report,lines); return failed==0?0:1;
    }
    internal static string Get(JsonElement element,string name)=>element.TryGetProperty(name,out var value)?value.ToString():"";
    internal static async Task<JsonDocument> Probe(string file)
    {
        string probe=ConversionService.Locate("ffprobe")??Path.Combine(Path.GetDirectoryName(ConversionService.Locate("ffmpeg"))!,"ffprobe.exe");
        return JsonDocument.Parse(await Command(probe,new[]{"-v","error","-show_streams","-show_format","-of","json",file}));
    }
    internal static async Task<string> Command(string exe,string[] args)
    {
        var info=new ProcessStartInfo(exe) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true };
        foreach(string arg in args) info.ArgumentList.Add(arg);
        using var process=Process.Start(info)!; var stdout=process.StandardOutput.ReadToEndAsync(); var stderr=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(40)); using var reg=timeout.Token.Register(()=> { try { process.Kill(true); } catch(InvalidOperationException) { } });
        await process.WaitForExitAsync(); string output=await stdout,error=await stderr;
        if(process.ExitCode!=0) throw new Exception(error); return output;
    }
}

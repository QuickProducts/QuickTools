using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace QuickTools;
internal sealed record MediaInfo(string Container,string? VideoCodec,string? PixelFormat,int Width,int Height,int? VideoIndex,int? AudioIndex,string? AudioCodec,double Duration,int Channels,double VideoDuration,double AudioDuration);
internal static class MediaConverter
{
    internal static async Task<MediaInfo> Probe(string file,CancellationToken token)
    {
        string probe=ToolLocator.Find("ffprobe")??throw new FileNotFoundException("Рядом с FFmpeg не найден ffprobe.exe. Переустановите FFmpeg в настройках.");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var result=await ProcessRunner.Run(probe,new[]{"-v","error","-show_streams","-show_format","-of","json",file},timeout.Token);
        if(result.ExitCode!=0) throw new InvalidDataException("Не удалось прочитать медиафайл: "+result.Error.Trim());
        using var json=JsonDocument.Parse(result.Output); var root=json.RootElement;
        var streams=root.GetProperty("streams").EnumerateArray().ToArray();
        var videos=streams.Where(s=>Get(s,"codec_type")=="video" && (!s.TryGetProperty("disposition",out var d) || !d.TryGetProperty("attached_pic",out var p) || p.GetInt32()==0)).ToArray();
        var audios=streams.Where(s=>Get(s,"codec_type")=="audio").ToArray();
        var video=Preferred(videos); var audio=Preferred(audios);
        var format=root.GetProperty("format");
        double.TryParse(Get(format,"duration"),NumberStyles.Any,CultureInfo.InvariantCulture,out double duration);
        return new(Get(format,"format_name"),Get(video,"codec_name"),Get(video,"pix_fmt"),Number(video,"width"),Number(video,"height"),Index(video),Index(audio),Get(audio,"codec_name"),duration,Number(audio,"channels"),Duration(video),Duration(audio));
    }
    private static JsonElement Preferred(JsonElement[] streams)=>streams.FirstOrDefault(s=>s.TryGetProperty("disposition",out var d) && d.TryGetProperty("default",out var p) && p.GetInt32()==1) is var best && best.ValueKind!=JsonValueKind.Undefined?best:streams.FirstOrDefault();
    private static string Get(JsonElement element,string key)=>element.ValueKind==JsonValueKind.Object && element.TryGetProperty(key,out var value)?value.ToString():"";
    private static int Number(JsonElement element,string key)=>int.TryParse(Get(element,key),out int result)?result:0;
    private static int? Index(JsonElement element)=>element.ValueKind==JsonValueKind.Undefined?null:Number(element,"index");
    private static double Duration(JsonElement stream)
    {
        if(double.TryParse(Get(stream,"duration"),NumberStyles.Any,CultureInfo.InvariantCulture,out double duration)) return duration;
        if(stream.ValueKind==JsonValueKind.Object && stream.TryGetProperty("tags",out var tags) && TimeSpan.TryParse(Get(tags,"DURATION"),CultureInfo.InvariantCulture,out var value)) return value.TotalSeconds;
        return 0;
    }
    internal static async Task Convert(string file,string output,ToolAction action,CancellationToken token)
    {
        var source=await Probe(file,token);
        bool still=action.Extension=="png",gif=action.Extension=="gif";
        bool video=action.Extension is "mp4" or "webm" or "mkv" or "mov" or "avi" or "mpg" or "ts" || gif || still;
        if(video && source.VideoIndex==null) throw new InvalidDataException("В исходном файле нет видеодорожки.");
        if(!video && source.AudioIndex==null) throw new InvalidDataException("В исходном видео нет звуковой дорожки. Извлечь аудио невозможно.");
        var args=new List<string> { "-nostdin","-n","-xerror","-hide_banner","-loglevel","error","-threads","2","-filter_threads","2","-i",file };
        if(video) { args.AddRange(new[]{"-map",gif?"[gifout]":"0:"+source.VideoIndex}); if(!gif && !still && source.AudioIndex!=null) args.AddRange(new[]{"-map","0:"+source.AudioIndex}); }
        else args.AddRange(new[]{"-map","0:"+source.AudioIndex});
        args.AddRange(new[]{"-sn","-dn"});
        args.AddRange(action.Arguments.Select(argument=>gif?argument.Replace("[0:v]","[0:"+source.VideoIndex+"]"):argument));
        if(video && !gif && !still)
        {
            args.AddRange(new[]{"-vf","pad=ceil(iw/2)*2:ceil(ih/2)*2","-pix_fmt","yuv420p"});
            if(action.Extension=="webm") args.AddRange(new[]{"-deadline","good","-cpu-used","3","-row-mt","1","-ar","48000"});
        }
        args.AddRange(new[]{"-threads","2",output});
        string ffmpeg=ToolLocator.Find("ffmpeg")!;
        var result=await ProcessRunner.Run(ffmpeg,args.ToArray(),token);
        if(result.ExitCode!=0) throw new IOException("FFmpeg: "+result.Error.Trim());
        var target=await Probe(output,token);
        if(video && target.VideoIndex==null) throw new InvalidDataException("Конвертер потерял видеодорожку.");
        if((!video || (!gif && !still && source.AudioIndex!=null)) && target.AudioIndex==null) throw new InvalidDataException("Конвертер потерял звуковую дорожку.");
        double expected=video?Math.Max(source.VideoDuration,source.AudioDuration):source.AudioDuration; if(expected<=0) expected=source.Duration;
        if(!gif && !still && expected>0 && target.Duration>0 && Math.Abs(target.Duration-expected)>Math.Max(.5,expected*.02)) throw new InvalidDataException("Длительность результата отличается от исходника.");
        if(action.Extension=="webm" && (!target.Container.Contains("webm") || target.VideoCodec!="vp9" || (source.AudioIndex!=null && target.AudioCodec!="opus"))) throw new InvalidDataException("Получен неверный формат WebM: ожидались VP9 и Opus.");
        if(action.Extension=="mp4" && (!target.Container.Contains("mp4") || target.VideoCodec!="h264")) throw new InvalidDataException("Получен неверный формат MP4.");
    }
}

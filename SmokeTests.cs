using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace QuickTools;
internal static class SmokeTests
{
    internal static async Task<int> Run(string report)
    {
        string dir=Path.Combine(Path.GetTempPath(),"QuickTools-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach(var (w,h,dpi) in new[]{(800d,600d,1d),(1280d,720d,1.5),(1920d,1080d,2d),(3840d,2160d,3d)})
                foreach(var (designW,designH) in new[]{(1060d,660d),(1120d,790d),(1000d,690d)})
                {
                    var fit=WindowLayout.Fit(designW,designH,w/dpi,h/dpi);
                    Require(fit.Width*dpi<=w && fit.Height*dpi<=h,"Window fits physical work area and DPI");
                    Require(Math.Abs(fit.Width/fit.Height-designW/designH)<.001,"Whole UI keeps aspect ratio");
                }
            var flow=new FirstRunFlow(); Require(flow.Step==WelcomeStep.Welcome && !flow.Completed,"First-run welcome");
            flow.Next(); Require(flow.Step==WelcomeStep.Components,"Component selection precedes setup"); flow.Next();
            bool gated=false; try { flow.Next(); } catch(InvalidOperationException) { gated=true; } Require(gated,"Cannot bypass running installation");
            flow.Retry(); Require(flow.Step==WelcomeStep.Components,"Cancelled installation returns to selection"); flow.Next(); flow.Prepared();
            Require(flow.Step==WelcomeStep.Trigger && !flow.Completed,"Tour follows installation"); flow.Next(); flow.Next(); flow.Next(); Require(!flow.Completed,"Not completed until final action"); flow.Next(); Require(flow.Completed,"Onboarding completion");
            Require(new FirstRunFlow(true).Step==WelcomeStep.Trigger,"Replay starts with tour");
            var stored=new UserSettings { FirstRunCompleted=true,Accent="#B6A5FA" };
            var loaded=System.Text.Json.JsonSerializer.Deserialize<UserSettings>(System.Text.Json.JsonSerializer.Serialize(stored));
            Require(loaded?.FirstRunCompleted==true && loaded.Accent=="#B6A5FA","First-run completion persists with preferences");
            foreach(string example in new[]{"demo.mp4","demo.png","demo.txt"})
            {
                var all=Actions.For(new[]{example}); var options=MenuPages.Get(all,0); var surface=new GlassSurface(options,1);
                var seen=new System.Collections.Generic.HashSet<string>();
                for(int page=0;page<MenuPages.Count(all);page++)
                {
                    var entries=MenuPages.Get(all,page); Require(entries.Length<=9,"Readable page capacity");
                    foreach(var entry in entries.Where(a=>a.Engine!="page")) Require(seen.Add(Actions.Id(entry)),"Unique paged actions");
                    foreach(var entry in entries.Where(a=>a.Engine=="page")) Require(int.Parse(entry.Arguments[0])<MenuPages.Count(all),"Page navigation bounds");
                }
                Require(seen.Count==all.Length,"All formats reachable through pages");
                Require(surface.Hit(new System.Windows.Point(220,220))==-1,"Hub is not actionable");
                Require(surface.Hit(new System.Windows.Point(220,70))==0,"Top sector");
                for(int i=0;i<options.Length;i++)
                {
                    double angle=(-90+i*360.0/options.Length)*Math.PI/180;
                    Require(surface.Hit(new System.Windows.Point(220+134*Math.Cos(angle),220+134*Math.Sin(angle)))==i,"Sector geometry");
                }
                Require(surface.Hit(new System.Windows.Point(0,0))==-1,"Outside ring");
            }
            var invalid=new UserSettings { MenuScale=double.NaN,GlassOpacity=8,HoverDelay=-1,Accent="invalid" }; invalid.Validate();
            Require(invalid.MenuScale==1 && invalid.GlassOpacity==.95 && invalid.HoverDelay==150 && invalid.Accent=="#8DDDD0","Settings validation");
            using var service=new ConversionService();
            string file=Path.Combine(dir,"проверка пробел.txt"); await File.WriteAllTextAsync(file,"QuickTools ✓");
            var actions=Actions.For(new[]{file});
            string status="";
            await service.Run(new[]{file},actions.Single(a=>a.Engine=="zip"),s=>status=s);
            Require(File.Exists(Path.Combine(dir,"проверка пробел_converted.zip")),status);
            using(var archive=ZipFile.OpenRead(Path.Combine(dir,"проверка пробел_converted.zip")))
            using(var reader=new StreamReader(archive.Entries.Single().Open()))
                Require(await reader.ReadToEndAsync()=="QuickTools ✓","ZIP round trip");
            await service.Run(new[]{file},actions.Single(a=>a.Engine=="hash"),_=>{});
            string expected=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(file))).ToLowerInvariant();
            Require((await File.ReadAllTextAsync(Path.Combine(dir,"проверка пробел_converted.sha256"))).StartsWith(expected),"SHA256");
            await service.Run(new[]{file},actions.Single(a=>a.Engine=="zip"),_=>{});
            Require(File.Exists(Path.Combine(dir,"проверка пробел_converted_1.zip")),"Collision handling");
            Require(await File.ReadAllTextAsync(file)=="QuickTools ✓","Original preserved");
            foreach(var action in actions.Where(a=>a.Engine=="copytext"))
            {
                await service.Run(new[]{file},action,_=>{});
                Require((await File.ReadAllBytesAsync(Path.Combine(dir,"проверка пробел_converted."+action.Extension))).SequenceEqual(await File.ReadAllBytesAsync(file)),"Text bytes preserved: "+action.Extension);
            }
            Require(!File.Exists(Path.Combine(dir,"проверка пробел_converted.bat.exe")),"No executable wrapping");
            await service.Run(new[]{file},actions.Single(a=>a.Engine=="gzip"),_=>{});
            using(var gzip=new GZipStream(File.OpenRead(Path.Combine(dir,"проверка пробел_converted.gz")),CompressionMode.Decompress))
            using(var reader=new StreamReader(gzip)) Require(await reader.ReadToEndAsync()=="QuickTools ✓","GZIP round trip");
            await service.Run(new[]{file},actions.Single(a=>a.Engine=="tar"),_=>{});
            using(var tar=new System.Formats.Tar.TarReader(File.OpenRead(Path.Combine(dir,"проверка пробел_converted.tar"))))
            { var entry=tar.GetNextEntry(); Require(entry?.Name==Path.GetFileName(file),"TAR entry name"); using var reader=new StreamReader(entry!.DataStream!); Require(await reader.ReadToEndAsync()=="QuickTools ✓","TAR round trip"); }
            string empty=Path.Combine(dir,"empty.txt"); await File.WriteAllTextAsync(empty,"");
            await service.Run(new[]{empty},Actions.For(new[]{empty}).Single(a=>a.Engine=="copytext" && a.Extension=="bat"),_=>{});
            Require(File.Exists(Path.Combine(dir,"empty_converted.bat")) && new FileInfo(Path.Combine(dir,"empty_converted.bat")).Length==0,"Empty text copy");
            string media="SKIPPED (ffmpeg absent)";
            if(ConversionService.Locate("ffmpeg")!=null)
            {
                string wav=Path.Combine(dir,"тишина.wav");
                using(var writer=new BinaryWriter(File.Create(wav)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(16036); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000); writer.Write((short)2); writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(16000); writer.Write(new byte[16000]);
                }
                await service.Run(new[]{wav},Actions.For(new[]{wav}).Single(a=>a.Extension=="flac"),_=>{});
                string flac=Path.Combine(dir,"тишина_converted.flac");
                Require(File.Exists(flac) && new FileInfo(flac).Length>0,"FFmpeg WAV to FLAC");
                media="PASS WAV → FLAC";
            }
            // A corrupt media file must leave neither output reservations nor staging folders.
            string broken=Path.Combine(dir,"broken.wav"); await File.WriteAllTextAsync(broken,"invalid audio");
            await service.Run(new[]{broken},Actions.For(new[]{broken}).Single(a=>a.Extension=="flac"),_=>{});
            Require(!File.Exists(Path.Combine(dir,"broken_converted.flac")) && Directory.GetDirectories(dir).Length==0,"Failure cleanup");
            string documents="SKIPPED (Pandoc absent)";
            // The real Shell worker is disposable and must return even for a non-Shell point.
            using(var queries=new ShellQueryService())
            {
                var budget=System.Diagnostics.Stopwatch.StartNew();
                var reply=await queries.Resolve(new Native.POINT { X=-30000,Y=-30000 },false,System.Threading.CancellationToken.None);
                Require(reply.Paths.Length==0 && budget.ElapsedMilliseconds<4500,"Isolated Shell request is bounded");
            }
            if(ConversionService.Locate("pandoc")!=null)
            {
                string md=Path.Combine(dir,"Документ тест.md"); await File.WriteAllTextAsync(md,"# QuickTools\n\nПривет, мир!\n\n- Один\n- Два\n");
                await service.Run(new[]{md},Actions.For(new[]{md}).Single(a=>a.Engine=="pandoc" && a.Extension=="docx"),s=>status=s);
                string docx=Path.Combine(dir,"Документ тест_converted.docx"); Require(File.Exists(docx),"Pandoc Markdown → DOCX: "+status);
                using(var archive=ZipFile.OpenRead(docx)) Require(archive.GetEntry("word/document.xml")!=null,"Valid DOCX package");
                foreach(string ext in new[]{"txt","html","epub","rtf","odt"})
                {
                    await service.Run(new[]{docx},Actions.For(new[]{docx}).Single(a=>a.Engine=="pandoc" && a.Extension==ext),s=>status=s);
                    Require(File.Exists(Path.Combine(dir,"Документ тест_converted_converted."+ext)),"Pandoc DOCX → "+ext+": "+status);
                }
                if(ConversionService.Locate("typst")!=null)
                {
                    await service.Run(new[]{docx},Actions.For(new[]{docx}).Single(a=>a.Engine=="pandocpdf"),s=>status=s);
                    string pdf=Path.Combine(dir,"Документ тест_converted_converted.pdf");
                    Require(File.Exists(pdf) && System.Text.Encoding.ASCII.GetString((await File.ReadAllBytesAsync(pdf)).Take(5).ToArray())=="%PDF-","Pandoc + Typst DOCX → PDF: "+status);
                    if(ConversionService.Locate("pdftotext")!=null)
                    {
                        await service.Run(new[]{pdf},Actions.For(new[]{pdf}).Single(a=>a.Engine=="pdftotext"),s=>status=s);
                        string text=Path.Combine(dir,"Документ тест_converted_converted_converted.txt");
                        Require(File.Exists(text) && (await File.ReadAllTextAsync(text)).Contains("Привет"),"PDF preserves Cyrillic: "+status);
                    }
                    documents="PASS: MD → DOCX → TXT/HTML/EPUB/RTF/ODT/PDF; Cyrillic PDF round trip";
                }
                else documents="PASS: MD → DOCX → TXT/HTML/EPUB/RTF/ODT; PDF skipped (Typst absent)";
            }
            await File.WriteAllTextAsync(report,"PASS: window fitting at 100/150/200/300% DPI; bounded isolated Shell worker; first-run sequence and installation gate, retry, persisted completion, replay; paged radial hit testing, all formats reachable, page bounds, settings validation, 32 text extensions including TXT → BAT (byte-preserving copies), empty text, ZIP/TAR/GZIP round trips, SHA-256, collision naming, Unicode paths, originals preserved, failed conversion cleanup.\nFFmpeg: "+media+"\nDocuments: "+documents+"\nActual Explorer hover / multiple monitors require live verification.\n");
            return 0;
        }
        catch(Exception ex) { await File.WriteAllTextAsync(report,"FAIL: "+ex); return 1; }
        finally { Directory.Delete(dir,true); }
    }
    private static void Require(bool condition,string name) { if(!condition) throw new Exception(name); }
}

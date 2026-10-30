using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
namespace QuickTools;
internal static class ImageRegressionTests
{
    internal static async Task<int> Run(string report)
    {
        var lines=new List<string>(); int failed=0;
        string folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!,".build","image-fixtures"); Directory.CreateDirectory(folder);
        try
        {
            string magick=ConversionService.Locate("magick")??throw new Exception("ImageMagick absent");
            string png=Path.Combine(folder,"Прозрачное изображение.png");
            using(var bitmap=new System.Drawing.Bitmap(160,90))
            {
                using(var graphics=System.Drawing.Graphics.FromImage(bitmap)) { graphics.Clear(System.Drawing.Color.Transparent); graphics.FillRectangle(System.Drawing.Brushes.Red,20,20,100,50); }
                bitmap.Save(png,System.Drawing.Imaging.ImageFormat.Png);
            }
            using var service=new ConversionService();
            foreach(var action in Actions.For(new[]{png}).Where(a=>a.Engine=="magick"))
            {
                string prefix=Path.GetFileNameWithoutExtension(png)+"_converted";
                foreach(string previous in Directory.GetFiles(folder,prefix+"*."+action.Extension)) File.Delete(previous);
                var result=await service.Run(new[]{png},action,_=>{});
                try
                {
                    if(result.Errors.Length>0 || result.Outputs.Length!=1) throw new Exception(string.Join("; ",result.Errors));
                    string output=result.Outputs[0];
                    if(action.Extension=="pdf")
                    {
                        if(System.Text.Encoding.ASCII.GetString((await File.ReadAllBytesAsync(output)).Take(5).ToArray())!="%PDF-") throw new Exception("Not a PDF");
                    }
                    else
                    {
                        string metadata=await MediaRegressionTests.Command(magick,new[]{"identify","-format","%m %w %h",output+"[0]"});
                        if(string.IsNullOrWhiteSpace(metadata)) throw new Exception("Cannot decode image");
                        if(action.Extension=="ico" && !metadata.Contains("256 256")) throw new Exception("ICO is not square 256×256");
                        if(action.Extension=="jpg")
                        {
                            using var jpeg=new System.Drawing.Bitmap(output); var color=jpeg.GetPixel(0,0);
                            if(color.R<245 || color.G<245 || color.B<245) throw new Exception("Transparent JPEG background is not white");
                        }
                        lines.Add("PASS PNG → "+action.Extension+": "+metadata); continue;
                    }
                    lines.Add("PASS PNG → "+action.Extension);
                }
                catch(Exception ex) { failed++; lines.Add("FAIL PNG → "+action.Extension+": "+ex.Message); }
            }
        }
        catch(Exception ex) { failed++; lines.Add("FAIL: "+ex); }
        lines.Add("Total failures: "+failed); await File.WriteAllLinesAsync(report,lines); return failed==0?0:1;
    }
}

using System.Windows;
using System.Windows.Media;
namespace QuickTools;
internal sealed class VectorIcon : FrameworkElement
{
    internal string Kind { get; set; }
    internal Brush Ink { get; set; }=Brushes.White;
    internal VectorIcon(string kind,double size=24) { Kind=kind; Width=Height=size; IsHitTestVisible=false; }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); dc.PushTransform(new ScaleTransform(ActualWidth/24,ActualHeight/24));
        var pen=new Pen(Ink,1.65) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round };
        string path=Kind switch
        {
            "video"=>"M4,5 L15,5 Q17,5 17,7 L17,17 Q17,19 15,19 L4,19 Q2,19 2,17 L2,7 Q2,5 4,5 M17,9 L22,6 L22,18 L17,15 M8,9 L12,12 L8,15 Z",
            "audio"=>"M9,17 L9,5 L20,3 L20,15 M9,9 L20,7 M9,17 C9,21 2,22 2,18 C2,15 9,14 9,17 M20,15 C20,19 13,20 13,16 C13,13 20,12 20,15",
            "image"=>"M5,3 L19,3 Q21,3 21,5 L21,19 Q21,21 19,21 L5,21 Q3,21 3,19 L3,5 Q3,3 5,3 M3,16 L8,11 L13,16 L16,13 L21,18 M15,7 A1.5,1.5 0 1 1 14.99,7",
            "zip"=>"M6,2 L15,2 L20,7 L20,20 Q20,22 18,22 L6,22 Q4,22 4,20 L4,4 Q4,2 6,2 M14,2 L14,8 L20,8 M10,3 L10,5 M12,5 L12,7 M10,7 L10,9 M12,9 L12,11 M10,13 L13,13 L13,17 L10,17 Z",
            "hash"=>"M9,3 L6,21 M17,3 L14,21 M3,9 L21,9 M2,15 L20,15",
            "clipboard"=>"M8,8 L18,8 Q20,8 20,10 L20,20 Q20,22 18,22 L8,22 Q6,22 6,20 L6,10 Q6,8 8,8 M16,4 L16,2 L3,2 L3,17 L6,17",
            "text"=>"M6,2 L15,2 L20,7 L20,20 Q20,22 18,22 L6,22 Q4,22 4,20 L4,4 Q4,2 6,2 M14,2 L14,8 L20,8 M8,12 L16,12 M8,16 L16,16",
            "settings"=>"M9,3 L15,3 L16,6 L19,8 L22,8 L22,14 L19,15 L17,18 L16,21 L10,21 L9,18 L6,16 L3,16 L3,10 L6,9 L8,6 Z M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12",
            "cursor"=>"M5,2 L20,13 L13,14 L10,21 Z M14,15 L18,21",
            "spark"=>"M12,2 L15,9 L22,12 L15,15 L12,22 L9,15 L2,12 L9,9 Z",
            "check"=>"M5,12 L10,17 L20,6",
            "close"=>"M6,6 L18,18 M18,6 L6,18",
            "next"=>"M4,12 L20,12 M14,5 L21,12 L14,19",
            "back"=>"M20,12 L4,12 M10,5 L3,12 L10,19",
            "code"=>"M8,6 L2,12 L8,18 M16,6 L22,12 L16,18 M14,3 L10,21",
            _=>"M6,2 L15,2 L20,7 L20,20 Q20,22 18,22 L6,22 Q4,22 4,20 L4,4 Q4,2 6,2 M14,2 L14,8 L20,8 M8,14 L16,14 M12,10 L12,18"
        };
        dc.DrawGeometry(null,pen,Geometry.Parse(path)); dc.Pop();
    }
    internal static string For(ToolAction a)=>a.Engine switch { "page"=>a.Label=="Назад"?"back":"next","copytext"=>"code","zip" or "tar" or "gzip"=>"zip","hash"=>"hash","clipboard"=>"clipboard","text" or "pandoc" or "pandocpdf" or "pdftotext"=>"text","magick" or "pdfimage"=>"image",_=>a.Extension is "mp3" or "wav" or "m4a" or "flac" or "opus" or "aiff" or "wma" or "ac3" or "aac" or "ogg"?"audio":"video" };
}

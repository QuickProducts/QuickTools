using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
namespace QuickTools;
internal sealed class GlassSurface : Canvas
{
    internal const double Center=220,Radius=186,Inner=82;
    private readonly System.Windows.Shapes.Path[] sectors;
    private readonly StackPanel[] labels;
    private readonly ToolAction[] actions;
    private readonly TextBlock title,subtitle;
    private readonly string restingTitle;
    private string restingSubtitle="ВЫБЕРИТЕ ДЕЙСТВИЕ";
    private readonly Ellipse light;
    private int selected=-1;
    private readonly Color accent;
    internal GlassSurface(ToolAction[] options,int count,ImageSource? backdrop=null)
    {
        Width=Height=Center*2; actions=options; sectors=new System.Windows.Shapes.Path[options.Length]; labels=new StackPanel[options.Length];
        var settings=UserSettings.Current; accent=(Color)ColorConverter.ConvertFromString(settings.Accent);
        var disk=new EllipseGeometry(new Point(Center,Center),Radius,Radius);
        var ring=new CombinedGeometry(GeometryCombineMode.Exclude,disk,new EllipseGeometry(new Point(Center,Center),Inner-7,Inner-7));
        Place(new Ellipse { Width=Radius*2+25,Height=Radius*2+25,Fill=new RadialGradientBrush(new GradientStopCollection { new(Color.FromArgb(100,0,0,0),.78),new(Colors.Transparent,1) }) },Center-Radius-12.5,Center-Radius-5);
        var frosted=new Grid { Width=Width,Height=Height,Clip=disk,IsHitTestVisible=false };
        frosted.Background=new LinearGradientBrush(Color.FromArgb(130,66,99,115),Color.FromArgb(100,54,45,87),40);
        frosted.Children.Add(new Rectangle { Fill=new SolidColorBrush(Color.FromArgb((byte)(settings.GlassOpacity*255),17,26,35)) }); Children.Add(frosted);
        Children.Add(new System.Windows.Shapes.Path { Data=ring,Fill=new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(58,255,255,255),0),new(Color.FromArgb(7,255,255,255),.36),new(Color.FromArgb(0,255,255,255),.63),new(Color.FromArgb(27,151,202,223),1) },new Point(0,0),new Point(1,1)),IsHitTestVisible=false });
        for(int i=0;i<options.Length;i++)
        {
            double step=360.0/options.Length,middle=-90+i*step;
            var sector=new System.Windows.Shapes.Path { Data=Wedge(middle-step/2+.65,middle+step/2-.65),Fill=Idle(),Stroke=Edge(),StrokeThickness=.8,RenderTransform=new TranslateTransform() };
            sectors[i]=sector; Children.Add(sector);
            var label=new StackPanel { Width=82,IsHitTestVisible=false,RenderTransform=new TranslateTransform() };
            label.Children.Add(new VectorIcon(VectorIcon.For(options[i]),23) { HorizontalAlignment=HorizontalAlignment.Center,Ink=new SolidColorBrush(Color.FromRgb(228,237,244)) });
            label.Children.Add(new TextBlock { Text=options[i].Label,FontFamily=new FontFamily("Segoe UI Variable Text"),FontSize=11.5,FontWeight=FontWeights.Medium,Foreground=new SolidColorBrush(Color.FromRgb(228,237,244)),TextAlignment=TextAlignment.Center,Margin=new Thickness(0,9,0,0) });
            var p=Polar(134,middle); Place(label,p.X-41,p.Y-23); labels[i]=label;
        }
        Place(new Ellipse { Width=144,Height=144,Fill=new LinearGradientBrush(Color.FromArgb(160,68,84,98),Color.FromArgb(225,18,28,40),85),Stroke=Edge(),StrokeThickness=1.1 },Center-72,Center-72);
        double glowSize=24+settings.BlurRadius*2;
        light=new Ellipse { Width=glowSize,Height=glowSize,Fill=new RadialGradientBrush(Color.FromArgb(32,255,255,255),Colors.Transparent),IsHitTestVisible=false }; Place(light,Center-glowSize/2,Center-145);
        Place(new Image { Source=BrandAssets.Logo,Width=38,Height=38,IsHitTestVisible=false },Center-19,Center-52);
        restingTitle=options.Length==0?"Нет действий":count==1?"1 файл":count+" файлов";
        title=new TextBlock { Width=140,Text=restingTitle,FontFamily=new FontFamily("Segoe UI Variable Display"),FontSize=16,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,TextAlignment=TextAlignment.Center };
        subtitle=new TextBlock { Width=140,Text="ВЫБЕРИТЕ ДЕЙСТВИЕ",FontSize=7.6,Foreground=new SolidColorBrush(Color.FromRgb(166,186,199)),TextAlignment=TextAlignment.Center,Margin=new Thickness(0,4,0,0) };
        Place(title,Center-70,Center-7); Place(subtitle,Center-70,Center+16);
        Place(new Ellipse { Width=Radius*2-2,Height=Radius*2-2,Stroke=Edge(),StrokeThickness=1.1,IsHitTestVisible=false },Center-Radius+1,Center-Radius+1);
        var caustic=new System.Windows.Shapes.Path { Data=Geometry.Parse("M48,160 A181,181 0 0 1 272,42"),Stroke=new LinearGradientBrush(Color.FromArgb(4,255,255,255),Color.FromArgb(170,235,255,255),0),StrokeThickness=2.3,IsHitTestVisible=false };
        Children.Add(caustic);
    }
    internal int Hit(Point p)
    {
        double x=p.X-Center,y=p.Y-Center,r=Math.Sqrt(x*x+y*y);
        if(actions.Length==0 || r<Inner || r>Radius) return -1;
        double step=360.0/actions.Length,angle=(Math.Atan2(y,x)*180/Math.PI+90+step/2+360)%360;
        return angle%step<.65 || angle%step>step-.65?-1:(int)(angle/step);
    }
    internal void Highlight(int index)
    {
        if(index==selected) return;
        if(selected>=0) Animate(selected,false); selected=index; if(index>=0) Animate(index,true);
        title.Text=index<0?restingTitle:actions[index].Label;
        subtitle.Text=index<0?restingSubtitle:actions[index].Engine switch { "clipboard"=>"КОПИРОВАТЬ ПУТИ","copytext"=>"КОПИЯ ТЕКСТА · ."+actions[index].Extension.ToUpperInvariant(),"page"=>"ДРУГИЕ ФОРМАТЫ",_=>"НАЖМИТЕ ДЛЯ ВЫБОРА" };
    }
    internal void SetPage(int page,int count) { restingSubtitle=count>1?$"СТРАНИЦА {page+1} / {count}":"ВЫБЕРИТЕ ДЕЙСТВИЕ"; subtitle.Text=restingSubtitle; }
    internal void MoveLight(Point p) { SetLeft(light,Center-17+(p.X-Center)*.15); SetTop(light,Center-120+(p.Y-Center)*.12); }
    private void Animate(int index,bool active)
    {
        sectors[index].Fill=active?new LinearGradientBrush(Color.FromArgb(135,accent.R,accent.G,accent.B),Color.FromArgb(30,accent.R,accent.G,accent.B),70):Idle();
        sectors[index].Stroke=active?new SolidColorBrush(Color.FromArgb(190,accent.R,accent.G,accent.B)):Edge();
        double angle=(-90+index*360.0/actions.Length)*Math.PI/180,distance=active?4:0;
        var duration=TimeSpan.FromMilliseconds(UserSettings.Current.Animations?130:0);
        foreach(var element in new FrameworkElement[]{sectors[index],labels[index]})
        {
            var t=(TranslateTransform)element.RenderTransform;
            t.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(Math.Cos(angle)*distance,duration) { EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } });
            t.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(Math.Sin(angle)*distance,duration) { EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } });
        }
    }
    private void Place(UIElement e,double x,double y) { SetLeft(e,x); SetTop(e,y); Children.Add(e); }
    private static Brush Idle()=>new LinearGradientBrush(Color.FromArgb(20,255,255,255),Color.FromArgb(7,255,255,255),90);
    private static Brush Edge()=>new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(120,242,251,255),0),new(Color.FromArgb(20,242,251,255),.48),new(Color.FromArgb(65,188,218,236),1) },new Point(0,0),new Point(1,1));
    private static Point Polar(double r,double degrees)=>new(Center+r*Math.Cos(degrees*Math.PI/180),Center+r*Math.Sin(degrees*Math.PI/180));
    private static Geometry Wedge(double start,double end)
    {
        var g=new StreamGeometry(); using(var c=g.Open()) { c.BeginFigure(Polar(Inner,start),true,true); c.LineTo(Polar(Radius,start),true,false); c.ArcTo(Polar(Radius,end),new Size(Radius,Radius),0,end-start>180,SweepDirection.Clockwise,true,false); c.LineTo(Polar(Inner,end),true,false); c.ArcTo(Polar(Inner,start),new Size(Inner,Inner),0,end-start>180,SweepDirection.Counterclockwise,true,false); } g.Freeze(); return g;
    }
}

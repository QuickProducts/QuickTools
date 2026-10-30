using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace QuickTools;
internal sealed class UserSettings
{
    internal static string FilePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QuickTools","settings.json");
    internal static UserSettings Current { get; }=Load();
    public double MenuScale { get; set; }=1;
    public double GlassOpacity { get; set; }=.42;
    public double BlurRadius { get; set; }=18;
    public int HoverDelay { get; set; }=350;
    public bool HoverEnabled { get; set; }=true;
    public bool Animations { get; set; }=true;
    public bool Notifications { get; set; }=true;
    public bool AutoInstallTools { get; set; }=true;
    public bool FirstRunCompleted { get; set; }
    public string Accent { get; set; }="#8DDDD0";
    public Dictionary<string,string> ToolPaths { get; set; }=new();
    public List<string> HiddenActions { get; set; }=new();
    private static UserSettings Load()
    {
        try { var s=JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FilePath))??new(); s.Validate(); return s; }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    internal void Validate()
    {
        MenuScale=double.IsFinite(MenuScale)?Math.Clamp(MenuScale,.75,1.3):1;
        GlassOpacity=double.IsFinite(GlassOpacity)?Math.Clamp(GlassOpacity,.3,.95):.42;
        BlurRadius=double.IsFinite(BlurRadius)?Math.Clamp(BlurRadius,0,32):18;
        HoverDelay=Math.Clamp(HoverDelay,150,1200);
        if(Accent!="#8DDDD0" && Accent!="#B6A5FA" && Accent!="#91BFFF" && Accent!="#F0BA91") Accent="#8DDDD0";
        ToolPaths??=new(); HiddenActions??=new();
    }
    internal void Save()
    {
        Validate(); Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath+".tmp",JsonSerializer.Serialize(this,new JsonSerializerOptions { WriteIndented=true }));
        File.Move(FilePath+".tmp",FilePath,true);
    }
}

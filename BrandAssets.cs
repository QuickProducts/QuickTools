using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace QuickTools;
internal static class BrandAssets
{
    internal static ImageSource Logo { get; }=Load("quicktools-logo-glass.png",768);
    internal static ImageSource Banner { get; }=Load("quicktools-banner-glass.png",1536);
    private static ImageSource Load(string name,int width)
    {
        var image=new BitmapImage(); image.BeginInit(); image.UriSource=new Uri("pack://application:,,,/assets/"+name,UriKind.Absolute);
        image.CacheOption=BitmapCacheOption.OnLoad; image.DecodePixelWidth=width; image.EndInit(); image.Freeze(); return image;
    }
}

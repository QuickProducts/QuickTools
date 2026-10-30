using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace QuickTools;
internal static class ShellResolver
{
    // Called on a dedicated STA: Shell automation objects never cross apartments.
    internal static string[] Resolve(Native.POINT point, bool selection)
    {
        var root=selection?Native.GetForegroundWindow():Native.GetAncestor(Native.WindowFromPoint(point),2);
        var cls=Native.Class(root);
        if(cls!="CabinetWClass" && cls!="ExploreWClass" && cls!="Progman" && cls!="WorkerW") return Array.Empty<string>();
        var foreground=Native.GetForegroundWindow();
        if (root!=foreground && cls!="Progman" && cls!="WorkerW") return Array.Empty<string>();
        string? hovered=null;
        if(!selection)
        {
            var element=AutomationElement.FromPoint(new Point(point.X,point.Y));
            for(int i=0;element!=null && i<7;i++,element=TreeWalker.ControlViewWalker.GetParent(element))
            {
                var type=element.Current.ControlType;
                if(type==ControlType.ListItem || type==ControlType.DataItem) { hovered=element.Current.Name; break; }
            }
            if(string.IsNullOrWhiteSpace(hovered)) return Array.Empty<string>();
        }
        dynamic? shell=null, windows=null;
        try
        {
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            windows=shell!.Windows();
            if(cls=="Progman" || cls=="WorkerW")
            {
                // SWC_DESKTOP=8, SWFO_NEEDDISPATCH=1: actual desktop Shell view.
                object location=0, rootLocation=0; int hwnd;
                dynamic desktop=windows!.FindWindowSW(ref location,ref rootLocation,8,out hwnd,1);
                try { return Read(desktop.Document,hovered,selection); }
                finally { Release(desktop); }
            }
            for(int i=0;i<(int)windows!.Count;i++)
            {
                dynamic window=windows.Item(i);
                try { if(new IntPtr((long)window.HWND)==root) return Read(window.Document,hovered,selection); }
                finally { Release(window); }
            }
            return Array.Empty<string>();
        }
        finally { Release(windows); Release(shell); }
    }
    private static string[] Read(dynamic document,string? name,bool selection)
    {
        dynamic? folder=null,items=null;
        try
        {
            folder=document.Folder;
            if(!selection)
            {
                dynamic? direct=null;
                try
                {
                    direct=folder.ParseName(name);
                    if(direct!=null) { string path=direct.Path; if(File.Exists(path)) return new[]{path}; }
                }
                finally { Release(direct); }
            }
            items=selection?document.SelectedItems():folder.Items();
            // Filter in Shell, rather than asking COM for every item in a large directory.
            if(!selection) items.Filter(0x40,name+".*");
            int count=(int)items.Count;
            if(selection && count>512) throw new InvalidOperationException("Выберите не более 512 файлов за раз.");
            if(!selection && count>32) return Array.Empty<string>();
            var result=new List<string>();
            var budget=Stopwatch.StartNew();
            for(int i=0;i<count;i++)
            {
                if(budget.ElapsedMilliseconds>700) throw new TimeoutException("Проводник отвечает слишком долго");
                dynamic item=items.Item(i);
                try
                {
                    string path=item.Path, display=item.Name;
                    if(File.Exists(path) && (selection || display==name || Path.GetFileName(path)==name)) result.Add(path);
                }
                finally { Release(item); }
            }
            // A duplicate display name is ambiguous; never guess a file.
            return !selection && result.Count!=1?Array.Empty<string>():result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        finally { Release(items); Release(folder); Release(document); }
    }
    private static void Release(object? value) { if(value!=null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
}

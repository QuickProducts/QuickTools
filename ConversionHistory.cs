using System;
using System.Collections.Generic;
namespace QuickTools;
internal sealed record ConversionEntry(DateTime Time,string Source,string Action,string? Output,string? Error,bool UsedFallback=false);
internal static class ConversionHistory
{
    internal static bool PersistenceEnabled { get; set; }=true;
    private static readonly object gate=new();
    private static readonly List<ConversionEntry> entries=new();
    private static bool loaded;
    internal static int ActiveJobs { get; private set; }
    internal static event Action? Changed;
    internal static ConversionEntry[] Snapshot() { lock(gate) { Load(); return entries.ToArray(); } }
    internal static void JobStarted() { lock(gate) ActiveJobs++; Announce(); }
    internal static void JobFinished() { lock(gate) ActiveJobs=Math.Max(0,ActiveJobs-1); Announce(); }
    private static void Announce() { try { Changed?.Invoke(); } catch(Exception ex) { RuntimeLog.Write("History observer",ex); } }
    private static void Load()
    {
        if(loaded || !PersistenceEnabled) return; loaded=true;
        try
        {
            string file=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(UserSettings.FilePath)!,"conversions.jsonl");
            if(!System.IO.File.Exists(file)) return;
            var recent=new System.Collections.Generic.Queue<ConversionEntry>();
            foreach(string line in System.IO.File.ReadLines(file))
            {
                try { var entry=System.Text.Json.JsonSerializer.Deserialize<ConversionEntry>(line); if(entry!=null) { recent.Enqueue(entry); if(recent.Count>50) recent.Dequeue(); } } catch(System.Text.Json.JsonException) { }
            }
            entries.AddRange(recent); entries.Reverse();
        }
        catch(System.IO.IOException) { } catch(UnauthorizedAccessException) { }
    }
    internal static void Add(ConversionEntry entry)
    {
        lock(gate)
        {
            Load();
            entries.Insert(0,entry); if(entries.Count>50) entries.RemoveAt(entries.Count-1);
            try
            {
                if(PersistenceEnabled)
                {
                string folder=System.IO.Path.GetDirectoryName(UserSettings.FilePath)!; System.IO.Directory.CreateDirectory(folder);
                string path=System.IO.Path.Combine(folder,"conversions.jsonl"); if(System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length>524288) System.IO.File.WriteAllText(path,"");
                System.IO.File.AppendAllText(path,System.Text.Json.JsonSerializer.Serialize(entry)+Environment.NewLine);
                }
            }
            catch(System.IO.IOException) { } catch(UnauthorizedAccessException) { }
        }
        Announce();
    }
}

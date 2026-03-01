namespace EdfBrowser.App.Models;

public sealed class SessionModel
{
    public string? SessionFilePath { get; set; }
    public List<SessionFileEntry> Files { get; set; } = new();
    public List<SessionSignalEntry> Signals { get; set; } = new();
    public long ViewTimeTicks { get; set; }
    public long PageTimeTicks { get; set; }
    public int ZoomHistoryIndex { get; set; }
    public AppSettings Settings { get; set; } = new();
}

public sealed class SessionFileEntry
{
    public string FilePath { get; set; } = "";
    public long ViewTimeOffsetTicks { get; set; }
}

public sealed class SessionSignalEntry
{
    public int FileIndex { get; set; }
    public int SignalIndex { get; set; }
    public double Sensitivity { get; set; }
    public int ScreenOffset { get; set; }
    public int Polarity { get; set; } = 1;
    public string ColorHex { get; set; } = "#000000";
    public List<SessionFilterEntry> Filters { get; set; } = new();
    public List<string> PreMathFunctions { get; set; } = new();
    public List<string> PostMathFunctions { get; set; } = new();
}

public sealed class SessionFilterEntry
{
    public string FilterType { get; set; } = "";
    public Dictionary<string, string> Parameters { get; set; } = new();
}

namespace EdfBrowser.App.Models;

public sealed class MontageModel
{
    public string Name { get; set; } = "";
    public string? FilePath { get; set; }
    public List<MontageSignalEntry> Signals { get; set; } = new();
}

public sealed class MontageSignalEntry
{
    public string SignalLabel { get; set; } = "";
    public double Sensitivity { get; set; } = 1.0;
    public int ScreenOffset { get; set; }
    public int Polarity { get; set; } = 1;
    public string ColorHex { get; set; } = "#000000";
    public List<SessionFilterEntry> Filters { get; set; } = new();
    public List<string> PreMathFunctions { get; set; } = new();
    public List<string> PostMathFunctions { get; set; } = new();
}

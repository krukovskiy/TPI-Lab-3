namespace EdfBrowser.App.Models;

using EdfBrowser.Dsp.FilterPipeline;
using System.Windows.Media;

public sealed class SignalComponent
{
    public int FileIndex { get; set; }
    public int SignalIndex { get; set; }
    public string Label { get; set; } = "";
    public double Sensitivity { get; set; } = 1.0;
    public double VoltsPerCm { get; set; } = 100.0;
    public int ScreenOffset { get; set; }
    public int Polarity { get; set; } = 1;
    public Color TraceColor { get; set; } = Colors.Black;
    public bool Visible { get; set; } = true;
    public double SamplePixelRatio { get; set; }
    public int Baseline { get; set; }
    public FilterChain? FilterChain { get; set; }
    public double OldValue { get; set; }
    public long OldSampleNumber { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double PhysicalMinimum { get; set; }
    public double PhysicalMaximum { get; set; }
    public int DigitalMinimum { get; set; }
    public int DigitalMaximum { get; set; }
    public double BitValue { get; set; }
    public double Offset { get; set; }
    public double SampleFrequency { get; set; }
    public int SamplesPerDataRecord { get; set; }
    public string PhysicalDimension { get; set; } = "";
}

namespace EdfBrowser.App.Rendering;

public sealed class CoordinateTransform
{
    public double SamplePixelRatio { get; set; }
    public double Sensitivity { get; set; }
    public int Baseline { get; set; }
    public int ScreenOffset { get; set; }
    public int Polarity { get; set; } = 1;
    public double BitValue { get; set; }
    public double Offset { get; set; }
    public int ViewportWidth { get; set; }
    public int ViewportHeight { get; set; }

    public float SampleToPixelX(long sampleIndex, long viewStartSample) =>
        (float)((sampleIndex - viewStartSample) / SamplePixelRatio);

    public float ValueToPixelY(double physicalValue) =>
        (float)(Baseline - (physicalValue * Sensitivity * Polarity) + ScreenOffset);

    public float DigitalToPixelY(int digitalValue) =>
        ValueToPixelY((digitalValue + Offset) * BitValue);

    public long PixelToSample(double xPixel, long viewStartSample) =>
        (long)(xPixel * SamplePixelRatio) + viewStartSample;

    public double PixelToValue(double yPixel) =>
        (Baseline - yPixel + ScreenOffset) / (Sensitivity * Polarity);

    public double PixelToTimeSec(double xPixel, double sampleRate) =>
        xPixel * SamplePixelRatio / sampleRate;

    public float TimeSecToPixel(double timeSec, double sampleRate) =>
        (float)(timeSec * sampleRate / SamplePixelRatio);
}

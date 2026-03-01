namespace EdfBrowser.Converters.Import;

using EdfBrowser.Converters.Common;

public sealed class Biox2EdfConverter : ConverterBase
{
    public override string Name => "Biox to EDF";
    public override string Description => "Converts Biox signal data to EDF.";
    public override string[] SupportedExtensions => [".biox", ".bxf"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);

        var data = await File.ReadAllBytesAsync(inputPath, ct);
        int nCh = options?.SignalCount ?? 4;
        int sr = (int)(options?.SampleRate ?? 256);
        int hdrSize = 1024;
        long total = (data.Length - hdrSize) / (2 * nCh);
        double rd = options?.DataRecordDuration ?? 1.0;
        int spr = (int)(sr * rd);
        long nRec = Math.Max(1, (total + spr - 1) / spr);

        var hd = new EdfHeaderData { PatientId = options?.PatientId ?? "X", RecordingId = "Biox",
            StartDateTime = options?.StartDateTime ?? DateTime.Now, SignalCount = nCh,
            DataRecordDuration = rd, DataRecordCount = nRec };
        for (int i = 0; i < nCh; i++)
            hd.Signals.Add(new SignalData { Label = $"Ch{i + 1}", PhysicalDimension = "uV",
                PhysicalMin = -3200, PhysicalMax = 3200, DigitalMin = -32768, DigitalMax = 32767, SamplesPerRecord = spr });

        await using var o = File.Create(outputPath);
        using var w = new BinaryWriter(o);
        WriteEdfHeader(w, hd);
        for (long r = 0; r < nRec; r++) { ct.ThrowIfCancellationRequested();
            for (int c = 0; c < nCh; c++) for (int s = 0; s < spr; s++) {
                long idx = r * spr + s; int v = 0;
                long p = hdrSize + (idx * nCh + c) * 2;
                if (p + 1 < data.Length) v = BitConverter.ToInt16(data, (int)p);
                WriteEdfSample(w, v); } }
        ReportProgress(progress, 100, "Done");
    }
}

namespace EdfBrowser.Converters.Import;

using EdfBrowser.Converters.Common;

public sealed class Fino2EdfConverter : ConverterBase
{
    public override string Name => "Fino to EDF";
    public override string Description => "Converts Finapres/Finometer cardiovascular data to EDF.";
    public override string[] SupportedExtensions => [".fino", ".fin"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);

        var data = await File.ReadAllBytesAsync(inputPath, ct);
        int nCh = options?.SignalCount ?? 4; // BP, HR, SV, CO typically
        int sr = (int)(options?.SampleRate ?? 200);
        int hdrSize = 512;
        long total = (data.Length - hdrSize) / (2 * nCh);
        double rd = options?.DataRecordDuration ?? 1.0;
        int spr = (int)(sr * rd);
        long nRec = Math.Max(1, (total + spr - 1) / spr);
        string[] labels = ["BP", "HR", "SV", "CO", "TPR", "Ch6", "Ch7", "Ch8"];

        var hd = new EdfHeaderData { PatientId = options?.PatientId ?? "X", RecordingId = "Finapres",
            StartDateTime = options?.StartDateTime ?? DateTime.Now, SignalCount = nCh,
            DataRecordDuration = rd, DataRecordCount = nRec };
        for (int i = 0; i < nCh; i++)
            hd.Signals.Add(new SignalData { Label = i < labels.Length ? labels[i] : $"Ch{i + 1}",
                PhysicalDimension = "mmHg", PhysicalMin = -500, PhysicalMax = 500,
                DigitalMin = -32768, DigitalMax = 32767, SamplesPerRecord = spr });

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

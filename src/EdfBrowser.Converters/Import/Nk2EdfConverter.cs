namespace EdfBrowser.Converters.Import;

using System.Text;
using EdfBrowser.Converters.Common;

public sealed class Nk2EdfConverter : ConverterBase
{
    public override string Name => "Nihon Kohden to EDF";
    public override string Description => "Converts Nihon Kohden EEG format (.eeg/.21e) to EDF.";
    public override string[] SupportedExtensions => [".eeg", ".21e"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);

        string dir = Path.GetDirectoryName(inputPath) ?? ".";
        string baseName = Path.GetFileNameWithoutExtension(inputPath);

        // NK format uses multiple files: .eeg (data), .21e (header), .pnt (patient), .log (events)
        string eegPath = Path.Combine(dir, baseName + ".eeg");
        if (!File.Exists(eegPath)) eegPath = inputPath;

        var data = await File.ReadAllBytesAsync(eegPath, ct);
        int nCh = options?.SignalCount ?? 21;
        int sr = (int)(options?.SampleRate ?? 200);

        // NK header is typically in first 17408 bytes
        int hdrSize = Math.Min(17408, data.Length / 4);
        long total = (data.Length - hdrSize) / (2 * nCh);
        double rd = options?.DataRecordDuration ?? 1.0;
        int spr = (int)(sr * rd);
        long nRec = Math.Max(1, (total + spr - 1) / spr);

        var hd = new EdfHeaderData { PatientId = options?.PatientId ?? "X", RecordingId = "Nihon Kohden",
            StartDateTime = options?.StartDateTime ?? DateTime.Now, SignalCount = nCh,
            DataRecordDuration = rd, DataRecordCount = nRec };
        for (int i = 0; i < nCh; i++)
            hd.Signals.Add(new SignalData { Label = $"EEG{i + 1}", PhysicalDimension = "uV",
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

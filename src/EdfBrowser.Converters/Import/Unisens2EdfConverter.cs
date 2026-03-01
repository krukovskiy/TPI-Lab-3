namespace EdfBrowser.Converters.Import;

using System.Xml.Linq;
using EdfBrowser.Converters.Common;

public sealed class Unisens2EdfConverter : ConverterBase
{
    public override string Name => "Unisens to EDF";
    public override string Description => "Converts Unisens format (XML + binary) to EDF.";
    public override string[] SupportedExtensions => [".xml"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);
        ReportProgress(progress, 0, "Reading Unisens metadata...");

        string dir = Path.GetDirectoryName(inputPath) ?? ".";
        var doc = XDocument.Load(inputPath);
        var root = doc.Root ?? throw new InvalidDataException("Invalid Unisens XML.");

        double sampleRate = options?.SampleRate ?? 256;
        var signalEntries = root.Elements().Where(e =>
            e.Name.LocalName == "signalEntry" || e.Name.LocalName == "SignalEntry").ToList();

        if (signalEntries.Count == 0) throw new InvalidDataException("No signal entries found in Unisens XML.");

        // Collect all signals
        var allData = new List<(string label, double sr, double[] samples)>();
        foreach (var entry in signalEntries)
        {
            ct.ThrowIfCancellationRequested();
            string? binFile = entry.Attribute("id")?.Value ?? entry.Attribute("fileFormat")?.Value;
            if (binFile == null) continue;

            string binPath = Path.Combine(dir, binFile);
            if (!File.Exists(binPath)) continue;

            double sr2 = double.TryParse(entry.Attribute("sampleRate")?.Value,
                System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : sampleRate;
            string label = entry.Attribute("id")?.Value ?? "Signal";

            var bytes = await File.ReadAllBytesAsync(binPath, ct);
            int nSamples = bytes.Length / 2;
            var samples = new double[nSamples];
            for (int i = 0; i < nSamples; i++)
                samples[i] = BitConverter.ToInt16(bytes, i * 2);

            allData.Add((label, sr2, samples));
        }

        if (allData.Count == 0) throw new InvalidDataException("No signal data found.");

        double rd = options?.DataRecordDuration ?? 1.0;
        long maxSamples = allData.Max(d => d.samples.Length);
        double effectiveSr = allData[0].sr;
        int spr = (int)(effectiveSr * rd);
        long nRec = Math.Max(1, (maxSamples + spr - 1) / spr);

        var hd = new EdfHeaderData
        {
            PatientId = options?.PatientId ?? "X", RecordingId = options?.RecordingId ?? "Unisens",
            StartDateTime = options?.StartDateTime ?? DateTime.Now,
            SignalCount = allData.Count, DataRecordDuration = rd, DataRecordCount = nRec,
        };
        foreach (var (label, sr2, samples) in allData)
        {
            double min = samples.Length > 0 ? samples.Min() : -3200;
            double max = samples.Length > 0 ? samples.Max() : 3200;
            if (min >= max) { min = -3200; max = 3200; }
            hd.Signals.Add(new SignalData { Label = label, PhysicalDimension = "uV",
                PhysicalMin = min, PhysicalMax = max, DigitalMin = -32768, DigitalMax = 32767,
                SamplesPerRecord = spr });
        }

        await using var o = File.Create(outputPath);
        using var w = new BinaryWriter(o);
        WriteEdfHeader(w, hd);
        for (long r = 0; r < nRec; r++) { ct.ThrowIfCancellationRequested();
            for (int c = 0; c < allData.Count; c++) { var sig = hd.Signals[c]; var samples = allData[c].samples;
                for (int s = 0; s < spr; s++) {
                    long idx = r * spr + s;
                    double v = idx < samples.Length ? samples[idx] : 0;
                    WriteEdfSample(w, PhysicalToDigital(v, sig.PhysicalMin, sig.PhysicalMax, sig.DigitalMin, sig.DigitalMax)); } } }
        ReportProgress(progress, 100, "Done");
    }
}

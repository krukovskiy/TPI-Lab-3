namespace EdfBrowser.Converters.Import;

using System.Globalization;
using System.Text;
using EdfBrowser.Converters.Common;

public sealed class Mit2EdfConverter : ConverterBase
{
    public override string Name => "MIT-BIH to EDF";
    public override string Description => "Converts MIT-BIH (PhysioNet) format (.hea + .dat) to EDF.";
    public override string[] SupportedExtensions => [".hea"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);

        ReportProgress(progress, 0, "Reading MIT header...");

        var heaLines = await File.ReadAllLinesAsync(inputPath, ct);
        if (heaLines.Length < 2) throw new InvalidDataException("Invalid .hea file.");

        // Parse record line
        var recordParts = heaLines[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        string recordName = recordParts[0];
        int nSignals = int.Parse(recordParts[1], CultureInfo.InvariantCulture);
        double sampleRate = recordParts.Length > 2 ? double.Parse(recordParts[2], CultureInfo.InvariantCulture) : 250;
        long totalSamples = recordParts.Length > 3 ? long.Parse(recordParts[3], CultureInfo.InvariantCulture) : 0;

        // Parse signal lines
        var labels = new string[nSignals];
        var gains = new double[nSignals];
        var baselines = new int[nSignals];
        var adcResolution = new int[nSignals];
        string? datFileName = null;
        int format = 16;

        for (int i = 0; i < nSignals && i + 1 < heaLines.Length; i++)
        {
            var parts = heaLines[i + 1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1) continue;
            if (i == 0) datFileName = parts[0];
            format = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 16;
            gains[i] = parts.Length > 2 ? ParseGain(parts[2]) : 200.0;
            adcResolution[i] = parts.Length > 3 ? int.Parse(parts[3], CultureInfo.InvariantCulture) : 12;
            baselines[i] = parts.Length > 4 ? int.Parse(parts[4], CultureInfo.InvariantCulture) : 0;
            labels[i] = parts.Length > 8 ? parts[8] : $"Ch{i + 1}";
        }

        // Read .dat file
        string datPath = Path.Combine(Path.GetDirectoryName(inputPath) ?? ".", datFileName ?? recordName + ".dat");
        if (!File.Exists(datPath)) throw new FileNotFoundException($"Data file not found: {datPath}");

        ReportProgress(progress, 20, "Reading signal data...");
        var datBytes = await File.ReadAllBytesAsync(datPath, ct);

        double recordDuration = options?.DataRecordDuration ?? 1.0;
        int samplesPerRecord = (int)(sampleRate * recordDuration);
        if (totalSamples == 0)
            totalSamples = format == 16 ? datBytes.Length / (2 * nSignals) : datBytes.Length * 3 / (nSignals * 2); // approximate for format 212
        long totalRecords = (totalSamples + samplesPerRecord - 1) / samplesPerRecord;

        var headerData = new EdfHeaderData
        {
            PatientId = options?.PatientId ?? "X",
            RecordingId = options?.RecordingId ?? recordName,
            StartDateTime = options?.StartDateTime ?? DateTime.Now,
            SignalCount = nSignals,
            DataRecordDuration = recordDuration,
            DataRecordCount = totalRecords,
        };

        for (int i = 0; i < nSignals; i++)
        {
            double physMax = gains[i] > 0 ? 32767.0 / gains[i] : 3200;
            headerData.Signals.Add(new SignalData
            {
                Label = labels[i],
                PhysicalDimension = "mV",
                PhysicalMin = -physMax,
                PhysicalMax = physMax,
                DigitalMin = -32768,
                DigitalMax = 32767,
                SamplesPerRecord = samplesPerRecord,
            });
        }

        ReportProgress(progress, 40, "Writing EDF...");

        await using var outStream = File.Create(outputPath);
        using var writer = new BinaryWriter(outStream);
        WriteEdfHeader(writer, headerData);

        using var ms = new MemoryStream(datBytes);
        using var reader = new BinaryReader(ms);

        for (long rec = 0; rec < totalRecords; rec++)
        {
            ct.ThrowIfCancellationRequested();
            for (int ch = 0; ch < nSignals; ch++)
            {
                for (int s = 0; s < samplesPerRecord; s++)
                {
                    long idx = rec * samplesPerRecord + s;
                    int sample = 0;
                    if (idx < totalSamples && format == 16)
                    {
                        long pos = (idx * nSignals + ch) * 2;
                        if (pos + 1 < datBytes.Length)
                        {
                            ms.Position = pos;
                            sample = reader.ReadInt16();
                        }
                    }
                    WriteEdfSample(writer, (short)Math.Clamp(sample - baselines[ch], -32768, 32767));
                }
            }
            ReportProgress(progress, 40 + (int)(60.0 * rec / totalRecords), "Writing...");
        }
        ReportProgress(progress, 100, "Done");
    }

    private static double ParseGain(string s)
    {
        int idx = s.IndexOf('(');
        if (idx > 0) s = s[..idx];
        idx = s.IndexOf('/');
        if (idx > 0) s = s[..idx];
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : 200.0;
    }
}

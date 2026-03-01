namespace EdfBrowser.Converters.Import;

using System.Globalization;
using EdfBrowser.Converters.Common;

public sealed class Ascii2EdfConverter : ConverterBase
{
    public override string Name => "ASCII to EDF";
    public override string Description => "Converts ASCII/CSV text files to EDF format.";
    public override string[] SupportedExtensions => [".csv", ".tsv", ".txt", ".asc"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);
        options ??= new ConverterOptions();

        ReportProgress(progress, 0, "Reading ASCII file...");

        var lines = await File.ReadAllLinesAsync(inputPath, ct);
        if (lines.Length == 0) throw new InvalidDataException("Empty input file.");

        string separator = options.Separator ?? DetectSeparator(lines[0]);
        int startLine = options.HasHeader ? 1 : 0;
        startLine += options.SkipLines ?? 0;

        // Parse data
        var data = new List<double[]>();
        int channelCount = 0;
        for (int i = startLine; i < lines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var parts = lines[i].Split(separator);
            if (channelCount == 0) channelCount = parts.Length;
            var row = new double[channelCount];
            for (int j = 0; j < Math.Min(parts.Length, channelCount); j++)
            {
                if (double.TryParse(parts[j].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                    row[j] = val;
            }
            data.Add(row);
        }

        if (data.Count == 0 || channelCount == 0)
            throw new InvalidDataException("No valid data found in ASCII file.");

        channelCount = options.SignalCount ?? channelCount;
        double sampleRate = options.SampleRate ?? 256.0;
        double recordDuration = options.DataRecordDuration ?? 1.0;
        int samplesPerRecord = (int)(sampleRate * recordDuration);

        ReportProgress(progress, 30, "Computing signal ranges...");

        // Compute physical ranges per channel
        var physMin = new double[channelCount];
        var physMax = new double[channelCount];
        for (int ch = 0; ch < channelCount; ch++) { physMin[ch] = double.MaxValue; physMax[ch] = double.MinValue; }
        foreach (var row in data)
        {
            for (int ch = 0; ch < channelCount; ch++)
            {
                if (row[ch] < physMin[ch]) physMin[ch] = row[ch];
                if (row[ch] > physMax[ch]) physMax[ch] = row[ch];
            }
        }
        for (int ch = 0; ch < channelCount; ch++)
        {
            if (physMin[ch] >= physMax[ch]) { physMin[ch] = -1; physMax[ch] = 1; }
        }

        // Parse labels from header
        string[]? labels = null;
        if (options.HasHeader && lines.Length > 0)
            labels = lines[0].Split(separator);

        ReportProgress(progress, 50, "Writing EDF file...");

        var headerData = new EdfHeaderData
        {
            PatientId = options.PatientId ?? "X",
            RecordingId = options.RecordingId ?? "X",
            StartDateTime = options.StartDateTime ?? DateTime.Now,
            SignalCount = channelCount,
            DataRecordDuration = recordDuration,
            DataRecordCount = (data.Count + samplesPerRecord - 1) / samplesPerRecord,
        };

        for (int ch = 0; ch < channelCount; ch++)
        {
            headerData.Signals.Add(new SignalData
            {
                Label = labels != null && ch < labels.Length ? labels[ch].Trim() : $"Ch{ch + 1}",
                PhysicalDimension = options.PhysicalDimension ?? "uV",
                PhysicalMin = options.PhysicalMin ?? physMin[ch],
                PhysicalMax = options.PhysicalMax ?? physMax[ch],
                DigitalMin = -32768,
                DigitalMax = 32767,
                SamplesPerRecord = samplesPerRecord,
            });
        }

        await using var fs = File.Create(outputPath);
        using var writer = new BinaryWriter(fs);
        WriteEdfHeader(writer, headerData);

        int totalSamples = data.Count;
        long totalRecords = headerData.DataRecordCount;
        for (long rec = 0; rec < totalRecords; rec++)
        {
            ct.ThrowIfCancellationRequested();
            for (int ch = 0; ch < channelCount; ch++)
            {
                var sig = headerData.Signals[ch];
                for (int s = 0; s < samplesPerRecord; s++)
                {
                    int idx = (int)(rec * samplesPerRecord + s);
                    double val = idx < totalSamples ? data[idx][ch] : 0;
                    int digital = PhysicalToDigital(val, sig.PhysicalMin, sig.PhysicalMax, sig.DigitalMin, sig.DigitalMax);
                    WriteEdfSample(writer, digital);
                }
            }
            ReportProgress(progress, 50 + (int)(50.0 * rec / totalRecords), "Writing data records...");
        }

        ReportProgress(progress, 100, "Done");
    }

    private static string DetectSeparator(string line)
    {
        if (line.Contains('\t')) return "\t";
        if (line.Contains(';')) return ";";
        return ",";
    }
}

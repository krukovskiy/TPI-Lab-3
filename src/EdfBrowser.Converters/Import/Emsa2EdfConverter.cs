namespace EdfBrowser.Converters.Import;

using EdfBrowser.Converters.Common;

public sealed class Emsa2EdfConverter : ConverterBase
{
    public override string Name => "EMSA to EDF";
    public override string Description => "Converts EMSA EEG format to EDF.";
    public override string[] SupportedExtensions => [".emsa", ".ems"];

    public override Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
        => ConvertBinaryFormatAsync(inputPath, outputPath, "EMSA EEG", options, progress, ct);

    private async Task ConvertBinaryFormatAsync(string inputPath, string outputPath,
        string formatName, ConverterOptions? options, IProgress<ConversionProgress>? progress,
        CancellationToken ct)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);

        var data = await File.ReadAllBytesAsync(inputPath, ct);
        int nChannels = options?.SignalCount ?? 21;
        int sampleRate = (int)(options?.SampleRate ?? 256);
        int headerSize = Math.Min(4096, data.Length / 4);
        long totalSamples = (data.Length - headerSize) / (2 * nChannels);
        double recordDuration = options?.DataRecordDuration ?? 1.0;
        int samplesPerRecord = (int)(sampleRate * recordDuration);
        long totalRecords = Math.Max(1, (totalSamples + samplesPerRecord - 1) / samplesPerRecord);

        var headerData = new EdfHeaderData
        {
            PatientId = options?.PatientId ?? "X", RecordingId = options?.RecordingId ?? formatName,
            StartDateTime = options?.StartDateTime ?? DateTime.Now, SignalCount = nChannels,
            DataRecordDuration = recordDuration, DataRecordCount = totalRecords,
        };
        for (int i = 0; i < nChannels; i++)
            headerData.Signals.Add(new SignalData { Label = $"EEG{i + 1}", PhysicalDimension = "uV",
                PhysicalMin = -3200, PhysicalMax = 3200, DigitalMin = -32768, DigitalMax = 32767,
                SamplesPerRecord = samplesPerRecord });

        await using var outStream = File.Create(outputPath);
        using var writer = new BinaryWriter(outStream);
        WriteEdfHeader(writer, headerData);

        for (long rec = 0; rec < totalRecords; rec++)
        {
            ct.ThrowIfCancellationRequested();
            for (int ch = 0; ch < nChannels; ch++)
                for (int s = 0; s < samplesPerRecord; s++)
                {
                    long idx = rec * samplesPerRecord + s;
                    int val = 0;
                    long pos = headerSize + (idx * nChannels + ch) * 2;
                    if (pos + 1 < data.Length) val = BitConverter.ToInt16(data, (int)pos);
                    WriteEdfSample(writer, val);
                }
            ReportProgress(progress, (int)(100.0 * rec / totalRecords), "Writing...");
        }
        ReportProgress(progress, 100, "Done");
    }
}

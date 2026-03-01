namespace EdfBrowser.Converters.Import;

using System.Globalization;
using EdfBrowser.Converters.Common;

public sealed class Raw2EdfConverter : ConverterBase
{
    public override string Name => "Raw Binary to EDF";
    public override string Description => "Converts raw binary signal data to EDF. Requires sample rate and channel count.";
    public override string[] SupportedExtensions => [".raw", ".bin", ".dat"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);
        options ??= new ConverterOptions();

        int channels = options.SignalCount ?? 1;
        double sampleRate = options.SampleRate ?? 256;
        string dataFormat = options.CustomOptions.GetValueOrDefault("format", "int16");
        int bytesPerSample = dataFormat switch { "int16" => 2, "int32" => 4, "float32" => 4, "float64" => 8, _ => 2 };

        ReportProgress(progress, 0, "Reading raw file...");

        var fileBytes = await File.ReadAllBytesAsync(inputPath, ct);
        long totalSamples = fileBytes.Length / (bytesPerSample * channels);
        double recordDuration = options.DataRecordDuration ?? 1.0;
        int samplesPerRecord = (int)(sampleRate * recordDuration);
        long totalRecords = (totalSamples + samplesPerRecord - 1) / samplesPerRecord;

        var headerData = new EdfHeaderData
        {
            PatientId = options.PatientId ?? "X",
            RecordingId = options.RecordingId ?? "X",
            StartDateTime = options.StartDateTime ?? DateTime.Now,
            SignalCount = channels,
            DataRecordDuration = recordDuration,
            DataRecordCount = totalRecords,
        };

        for (int ch = 0; ch < channels; ch++)
        {
            headerData.Signals.Add(new SignalData
            {
                Label = $"Ch{ch + 1}",
                PhysicalDimension = options.PhysicalDimension ?? "uV",
                PhysicalMin = options.PhysicalMin ?? -3200,
                PhysicalMax = options.PhysicalMax ?? 3200,
                DigitalMin = -32768,
                DigitalMax = 32767,
                SamplesPerRecord = samplesPerRecord,
            });
        }

        await using var outStream = File.Create(outputPath);
        using var writer = new BinaryWriter(outStream);
        WriteEdfHeader(writer, headerData);

        using var ms = new MemoryStream(fileBytes);
        using var reader = new BinaryReader(ms);

        for (long rec = 0; rec < totalRecords; rec++)
        {
            ct.ThrowIfCancellationRequested();
            for (int ch = 0; ch < channels; ch++)
            {
                var sig = headerData.Signals[ch];
                for (int s = 0; s < samplesPerRecord; s++)
                {
                    long idx = rec * samplesPerRecord + s;
                    double val = 0;
                    if (idx < totalSamples)
                    {
                        long pos = (idx * channels + ch) * bytesPerSample;
                        ms.Position = pos;
                        val = dataFormat switch
                        {
                            "int16" => reader.ReadInt16(),
                            "int32" => reader.ReadInt32(),
                            "float32" => reader.ReadSingle(),
                            "float64" => reader.ReadDouble(),
                            _ => reader.ReadInt16()
                        };
                    }
                    WriteEdfSample(writer, PhysicalToDigital(val, sig.PhysicalMin, sig.PhysicalMax, sig.DigitalMin, sig.DigitalMax));
                }
            }
            ReportProgress(progress, (int)(100.0 * rec / totalRecords), "Writing...");
        }
        ReportProgress(progress, 100, "Done");
    }
}

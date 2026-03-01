namespace EdfBrowser.Converters.Import;

using EdfBrowser.Converters.Common;

/// <summary>Converts Mortara ECG format to EDF. Mortara uses proprietary XML+binary format.</summary>
public sealed class Mortara2EdfConverter : ConverterBase
{
    public override string Name => "Mortara to EDF";
    public override string Description => "Converts Mortara ECG format to EDF.";
    public override string[] SupportedExtensions => [".xml", ".mor"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);
        ReportProgress(progress, 0, "Reading Mortara file...");

        // Mortara stores ECG data in XML with embedded binary sections
        // Parse the XML to find signal metadata and data sections
        var fileBytes = await File.ReadAllBytesAsync(inputPath, ct);
        int nLeads = options?.SignalCount ?? 12;
        int sampleRate = (int)(options?.SampleRate ?? 500);
        string[] leadNames = ["I", "II", "III", "aVR", "aVL", "aVF", "V1", "V2", "V3", "V4", "V5", "V6"];

        // Find binary data section (after XML header)
        int dataStart = FindBinaryDataOffset(fileBytes);
        long totalSamples = (fileBytes.Length - dataStart) / (2 * nLeads);

        double recordDuration = options?.DataRecordDuration ?? 1.0;
        int samplesPerRecord = (int)(sampleRate * recordDuration);
        long totalRecords = Math.Max(1, (totalSamples + samplesPerRecord - 1) / samplesPerRecord);

        ReportProgress(progress, 40, "Writing EDF...");

        var headerData = new EdfHeaderData
        {
            PatientId = options?.PatientId ?? "X",
            RecordingId = options?.RecordingId ?? "Mortara ECG",
            StartDateTime = options?.StartDateTime ?? DateTime.Now,
            SignalCount = nLeads,
            DataRecordDuration = recordDuration,
            DataRecordCount = totalRecords,
        };

        for (int i = 0; i < nLeads; i++)
            headerData.Signals.Add(new SignalData
            {
                Label = i < leadNames.Length ? leadNames[i] : $"Ch{i + 1}",
                PhysicalDimension = "uV", PhysicalMin = -3200, PhysicalMax = 3200,
                DigitalMin = -32768, DigitalMax = 32767, SamplesPerRecord = samplesPerRecord,
            });

        await using var outStream = File.Create(outputPath);
        using var writer = new BinaryWriter(outStream);
        WriteEdfHeader(writer, headerData);

        for (long rec = 0; rec < totalRecords; rec++)
        {
            ct.ThrowIfCancellationRequested();
            for (int ch = 0; ch < nLeads; ch++)
                for (int s = 0; s < samplesPerRecord; s++)
                {
                    long idx = rec * samplesPerRecord + s;
                    int val = 0;
                    long bytePos = dataStart + (idx * nLeads + ch) * 2;
                    if (bytePos + 1 < fileBytes.Length)
                        val = BitConverter.ToInt16(fileBytes, (int)bytePos);
                    WriteEdfSample(writer, val);
                }
        }
        ReportProgress(progress, 100, "Done");
    }

    private static int FindBinaryDataOffset(byte[] data)
    {
        // Look for end of XML (?>)  followed by binary data
        for (int i = 0; i < Math.Min(data.Length - 1, 65536); i++)
            if (data[i] == '?' && data[i + 1] == '>') return i + 2;
        return 0;
    }
}

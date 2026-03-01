namespace EdfBrowser.Converters.Import;

using EdfBrowser.Converters.Common;

public sealed class ScpEcg2EdfConverter : ConverterBase
{
    public override string Name => "SCP-ECG to EDF";
    public override string Description => "Converts SCP-ECG (Standard Communication Protocol for ECG) to EDF.";
    public override string[] SupportedExtensions => [".scp"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);
        ReportProgress(progress, 0, "Reading SCP-ECG file...");

        var fileBytes = await File.ReadAllBytesAsync(inputPath, ct);
        if (fileBytes.Length < 6) throw new InvalidDataException("File too small for SCP-ECG format.");

        // SCP-ECG: CRC (2 bytes) + file length (4 bytes) + sections
        int fileLen = BitConverter.ToInt32(fileBytes, 2);
        if (fileLen > fileBytes.Length) throw new InvalidDataException("Invalid SCP-ECG file length.");

        // Parse Section 0 (pointer section) to find other sections
        int pos = 6;
        int nLeads = 12; // Standard 12-lead ECG
        int sampleRate = 500;
        long totalSamples = 5000; // 10 seconds at 500 Hz default
        var leadData = new List<short[]>();

        // Simplified: try to find Section 6 (rhythm data)
        while (pos + 4 < fileBytes.Length)
        {
            ct.ThrowIfCancellationRequested();
            if (pos + 8 > fileBytes.Length) break;
            int sectionId = BitConverter.ToUInt16(fileBytes, pos);
            int sectionLen = BitConverter.ToInt32(fileBytes, pos + 2);
            if (sectionLen <= 0 || pos + sectionLen > fileBytes.Length) break;

            if (sectionId == 6 && sectionLen > 8) // Rhythm data section
            {
                int dataOffset = pos + 6;
                int samplesPerLead = (sectionLen - 6) / (2 * nLeads);
                totalSamples = samplesPerLead;
                for (int lead = 0; lead < nLeads; lead++)
                {
                    var samples = new short[samplesPerLead];
                    for (int s = 0; s < samplesPerLead; s++)
                    {
                        int bytePos = dataOffset + (s * nLeads + lead) * 2;
                        if (bytePos + 1 < fileBytes.Length)
                            samples[s] = BitConverter.ToInt16(fileBytes, bytePos);
                    }
                    leadData.Add(samples);
                }
                break;
            }
            pos += Math.Max(sectionLen, 6);
        }

        if (leadData.Count == 0)
        {
            // Fallback: treat as raw 12-lead 16-bit data after header
            nLeads = Math.Min(12, options?.SignalCount ?? 12);
            int dataStart = 6;
            int samplesPerLead = (fileBytes.Length - dataStart) / (2 * nLeads);
            totalSamples = samplesPerLead;
            for (int lead = 0; lead < nLeads; lead++)
            {
                var samples = new short[samplesPerLead];
                for (int s = 0; s < samplesPerLead; s++)
                {
                    int bytePos = dataStart + (s * nLeads + lead) * 2;
                    if (bytePos + 1 < fileBytes.Length)
                        samples[s] = BitConverter.ToInt16(fileBytes, bytePos);
                }
                leadData.Add(samples);
            }
        }

        nLeads = leadData.Count;
        string[] leadNames = ["I", "II", "III", "aVR", "aVL", "aVF", "V1", "V2", "V3", "V4", "V5", "V6"];

        ReportProgress(progress, 50, "Writing EDF...");

        double recordDuration = options?.DataRecordDuration ?? 1.0;
        int samplesPerRecord = (int)(sampleRate * recordDuration);
        long totalRecords = (totalSamples + samplesPerRecord - 1) / samplesPerRecord;

        var headerData = new EdfHeaderData
        {
            PatientId = options?.PatientId ?? "X",
            RecordingId = options?.RecordingId ?? "SCP-ECG",
            StartDateTime = options?.StartDateTime ?? DateTime.Now,
            SignalCount = nLeads,
            DataRecordDuration = recordDuration,
            DataRecordCount = totalRecords,
        };

        for (int i = 0; i < nLeads; i++)
        {
            headerData.Signals.Add(new SignalData
            {
                Label = i < leadNames.Length ? leadNames[i] : $"Lead{i + 1}",
                PhysicalDimension = "uV",
                PhysicalMin = -3200,
                PhysicalMax = 3200,
                DigitalMin = -32768,
                DigitalMax = 32767,
                SamplesPerRecord = samplesPerRecord,
            });
        }

        await using var outStream = File.Create(outputPath);
        using var writer = new BinaryWriter(outStream);
        WriteEdfHeader(writer, headerData);

        for (long rec = 0; rec < totalRecords; rec++)
        {
            ct.ThrowIfCancellationRequested();
            for (int ch = 0; ch < nLeads; ch++)
            {
                for (int s = 0; s < samplesPerRecord; s++)
                {
                    long idx = rec * samplesPerRecord + s;
                    short val = idx < leadData[ch].Length ? leadData[ch][idx] : (short)0;
                    WriteEdfSample(writer, val);
                }
            }
        }
        ReportProgress(progress, 100, "Done");
    }
}

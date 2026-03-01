namespace EdfBrowser.Converters.Import;

using System.Text;
using EdfBrowser.Converters.Common;

public sealed class Ishne2EdfConverter : ConverterBase
{
    public override string Name => "ISHNE to EDF";
    public override string Description => "Converts ISHNE Holter ECG format to EDF.";
    public override string[] SupportedExtensions => [".ecg", ".ish"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);
        ReportProgress(progress, 0, "Reading ISHNE header...");

        await using var inStream = File.OpenRead(inputPath);
        using var reader = new BinaryReader(inStream, Encoding.ASCII);

        // ISHNE header: magic "ISHNE1.0" (8 bytes) or check for it
        var magic = Encoding.ASCII.GetString(reader.ReadBytes(8));
        bool hasIshneHeader = magic.StartsWith("ISHNE");

        int varHeaderSize = 0, nLeads = 3, sampleRate = 1000;
        long dataOffset = 0, totalSamples = 0;

        if (hasIshneHeader)
        {
            reader.ReadBytes(2); // checksum
            varHeaderSize = reader.ReadInt32();
            reader.ReadInt32(); // sample size offset
            int nSamplesEcg = reader.ReadInt32();
            reader.ReadInt32(); // offset to var-length block
            int sizeOfVarBlock = reader.ReadInt32();
            reader.ReadBytes(80); // patient name
            reader.ReadBytes(4 * 3); // date fields
            nLeads = reader.ReadInt16();
            if (nLeads <= 0 || nLeads > 12) nLeads = 3;
            reader.ReadBytes(2); // lead spec
            reader.ReadBytes(2); // lead quality
            short[] resolution = new short[nLeads];
            for (int i = 0; i < 12; i++)
            {
                short r = reader.ReadInt16();
                if (i < nLeads) resolution[i] = r > 0 ? r : (short)1;
            }
            reader.ReadInt16(); // pacemaker
            reader.ReadBytes(40); // recorder type
            sampleRate = reader.ReadInt16();
            if (sampleRate <= 0) sampleRate = 1000;
            reader.ReadBytes(80); // proprietary
            reader.ReadBytes(80); // copyright

            dataOffset = 522 + varHeaderSize;
            totalSamples = (inStream.Length - dataOffset) / (2 * nLeads);
        }
        else
        {
            inStream.Position = 0;
            nLeads = options?.SignalCount ?? 3;
            sampleRate = (int)(options?.SampleRate ?? 1000);
            dataOffset = 0;
            totalSamples = inStream.Length / (2 * nLeads);
        }

        ReportProgress(progress, 30, "Writing EDF...");

        double recordDuration = options?.DataRecordDuration ?? 1.0;
        int samplesPerRecord = (int)(sampleRate * recordDuration);
        long totalRecords = (totalSamples + samplesPerRecord - 1) / samplesPerRecord;

        var headerData = new EdfHeaderData
        {
            PatientId = options?.PatientId ?? "X",
            RecordingId = options?.RecordingId ?? "ISHNE Holter",
            StartDateTime = options?.StartDateTime ?? DateTime.Now,
            SignalCount = nLeads,
            DataRecordDuration = recordDuration,
            DataRecordCount = totalRecords,
        };

        for (int i = 0; i < nLeads; i++)
            headerData.Signals.Add(new SignalData
            {
                Label = $"ECG{i + 1}", PhysicalDimension = "uV",
                PhysicalMin = -3200, PhysicalMax = 3200,
                DigitalMin = -32768, DigitalMax = 32767,
                SamplesPerRecord = samplesPerRecord,
            });

        await using var outStream = File.Create(outputPath);
        using var writer = new BinaryWriter(outStream);
        WriteEdfHeader(writer, headerData);

        inStream.Position = dataOffset;
        for (long rec = 0; rec < totalRecords; rec++)
        {
            ct.ThrowIfCancellationRequested();
            for (int ch = 0; ch < nLeads; ch++)
                for (int s = 0; s < samplesPerRecord; s++)
                {
                    long idx = rec * samplesPerRecord + s;
                    short val = idx < totalSamples ? reader.ReadInt16() : (short)0;
                    WriteEdfSample(writer, val);
                }
            ReportProgress(progress, 30 + (int)(70.0 * rec / totalRecords), "Writing...");
        }
        ReportProgress(progress, 100, "Done");
    }
}

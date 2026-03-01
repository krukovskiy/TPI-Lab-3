namespace EdfBrowser.Converters.Import;

using System.Text;
using EdfBrowser.Converters.Common;

public sealed class Biosemi2BdfPlusConverter : ConverterBase
{
    public override string Name => "Biosemi to BDF+";
    public override string Description => "Converts raw Biosemi BDF files to BDF+ format with proper metadata.";
    public override string[] SupportedExtensions => [".bdf"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);
        ReportProgress(progress, 0, "Reading Biosemi BDF...");

        await using var inStream = File.OpenRead(inputPath);
        using var reader = new BinaryReader(inStream, Encoding.Latin1);

        // Read BDF header
        byte firstByte = reader.ReadByte();
        if (firstByte != 0xFF) throw new InvalidDataException("Not a BDF file (first byte != 0xFF).");
        string biosemi = Encoding.Latin1.GetString(reader.ReadBytes(7));

        string patientId = Encoding.Latin1.GetString(reader.ReadBytes(80)).Trim();
        string recordingId = Encoding.Latin1.GetString(reader.ReadBytes(80)).Trim();
        string dateStr = Encoding.Latin1.GetString(reader.ReadBytes(8)).Trim();
        string timeStr = Encoding.Latin1.GetString(reader.ReadBytes(8)).Trim();
        int headerBytes = int.Parse(Encoding.Latin1.GetString(reader.ReadBytes(8)).Trim());
        string reserved = Encoding.Latin1.GetString(reader.ReadBytes(44)).Trim();
        long nRecords = long.Parse(Encoding.Latin1.GetString(reader.ReadBytes(8)).Trim());
        double duration = double.Parse(Encoding.Latin1.GetString(reader.ReadBytes(8)).Trim(),
            System.Globalization.CultureInfo.InvariantCulture);
        int nSignals = int.Parse(Encoding.Latin1.GetString(reader.ReadBytes(4)).Trim());

        // Read signal headers
        var labels = ReadFixedFields(reader, nSignals, 16);
        var transducers = ReadFixedFields(reader, nSignals, 80);
        var dimensions = ReadFixedFields(reader, nSignals, 8);
        var physMins = ReadFixedFields(reader, nSignals, 8);
        var physMaxs = ReadFixedFields(reader, nSignals, 8);
        var digMins = ReadFixedFields(reader, nSignals, 8);
        var digMaxs = ReadFixedFields(reader, nSignals, 8);
        var prefilters = ReadFixedFields(reader, nSignals, 80);
        var samplesPerRec = ReadFixedFields(reader, nSignals, 8);
        ReadFixedFields(reader, nSignals, 32); // reserved

        // Rewrite as BDF+ (add proper reserved field)
        ReportProgress(progress, 30, "Writing BDF+...");

        int nSignalsOut = nSignals + 1; // Add annotation channel
        int annotSamplesPerRec = (int)(duration * 3); // 3 bytes per annotation sample minimum
        if (annotSamplesPerRec < 60) annotSamplesPerRec = 60;

        var headerData = new EdfHeaderData
        {
            IsBdf = true,
            PatientId = options?.PatientId ?? (string.IsNullOrWhiteSpace(patientId) ? "X X X X" : patientId),
            RecordingId = options?.RecordingId ?? (string.IsNullOrWhiteSpace(recordingId) ? "Startdate X X X X" : recordingId),
            StartDateTime = options?.StartDateTime ?? ParseDateTime(dateStr, timeStr),
            SignalCount = nSignalsOut,
            DataRecordDuration = duration,
            DataRecordCount = nRecords,
        };

        for (int i = 0; i < nSignals; i++)
        {
            headerData.Signals.Add(new SignalData
            {
                Label = labels[i], TransducerType = transducers[i], PhysicalDimension = dimensions[i],
                PhysicalMin = double.TryParse(physMins[i], System.Globalization.CultureInfo.InvariantCulture, out var pmin) ? pmin : -3200,
                PhysicalMax = double.TryParse(physMaxs[i], System.Globalization.CultureInfo.InvariantCulture, out var pmax) ? pmax : 3200,
                DigitalMin = int.TryParse(digMins[i], out var dmin) ? dmin : -8388608,
                DigitalMax = int.TryParse(digMaxs[i], out var dmax) ? dmax : 8388607,
                Prefiltering = prefilters[i],
                SamplesPerRecord = int.TryParse(samplesPerRec[i], out var spr) ? spr : 256,
            });
        }

        // Add BDF Annotations channel
        headerData.Signals.Add(new SignalData
        {
            Label = "BDF Annotations", PhysicalDimension = "",
            PhysicalMin = -1, PhysicalMax = 1, DigitalMin = -8388608, DigitalMax = 8388607,
            SamplesPerRecord = annotSamplesPerRec,
        });

        await using var outStream = File.Create(outputPath);
        using var writer = new BinaryWriter(outStream);
        WriteEdfHeader(writer, headerData);

        // Copy data records, adding empty annotation channel
        inStream.Position = headerBytes;
        for (long rec = 0; rec < nRecords; rec++)
        {
            ct.ThrowIfCancellationRequested();
            // Copy all signal data
            for (int sig = 0; sig < nSignals; sig++)
            {
                int spr2 = int.TryParse(samplesPerRec[sig], out var sp) ? sp : 256;
                for (int s = 0; s < spr2; s++)
                    WriteBdfSample(writer, ReadBdfSample(reader));
            }
            // Write annotation TAL: onset time
            double onset = rec * duration;
            string tal = $"+{onset:F6}\x14\x14\0";
            var talBytes = System.Text.Encoding.UTF8.GetBytes(tal);
            int annotBytes = annotSamplesPerRec * 3;
            writer.Write(talBytes);
            for (int i = talBytes.Length; i < annotBytes; i++) writer.Write((byte)0);

            ReportProgress(progress, 30 + (int)(70.0 * rec / nRecords), "Writing...");
        }
        ReportProgress(progress, 100, "Done");
    }

    private static string[] ReadFixedFields(BinaryReader reader, int count, int fieldLen)
    {
        var result = new string[count];
        for (int i = 0; i < count; i++)
            result[i] = Encoding.Latin1.GetString(reader.ReadBytes(fieldLen)).Trim();
        return result;
    }

    private static DateTime ParseDateTime(string dateStr, string timeStr)
    {
        try
        {
            var dp = dateStr.Split('.');
            var tp = timeStr.Split('.');
            int year = int.Parse(dp[2]) + (int.Parse(dp[2]) > 84 ? 1900 : 2000);
            return new DateTime(year, int.Parse(dp[1]), int.Parse(dp[0]),
                int.Parse(tp[0]), int.Parse(tp[1]), int.Parse(tp[2]));
        }
        catch { return DateTime.Now; }
    }
}

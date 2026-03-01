namespace EdfBrowser.Converters.Import;

using System.Text;
using EdfBrowser.Converters.Common;

public sealed class Wav2EdfConverter : ConverterBase
{
    public override string Name => "WAV to EDF";
    public override string Description => "Converts WAV audio files to EDF format.";
    public override string[] SupportedExtensions => [".wav"];

    public override async Task ConvertAsync(string inputPath, string outputPath,
        ConverterOptions? options = null, IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ValidateInputFile(inputPath);
        ValidateOutputFile(outputPath);

        ReportProgress(progress, 0, "Reading WAV header...");

        await using var inStream = File.OpenRead(inputPath);
        using var reader = new BinaryReader(inStream, Encoding.ASCII, true);

        // Parse RIFF header
        string riffId = new(reader.ReadChars(4));
        if (riffId != "RIFF") throw new InvalidDataException("Not a valid WAV file: missing RIFF header.");
        reader.ReadInt32(); // file size
        string waveId = new(reader.ReadChars(4));
        if (waveId != "WAVE") throw new InvalidDataException("Not a valid WAV file: missing WAVE identifier.");

        // Find fmt chunk
        int channels = 0, sampleRate = 0, bitsPerSample = 0;
        int audioFormat = 0;
        long dataOffset = 0, dataSize = 0;

        while (inStream.Position < inStream.Length - 8)
        {
            string chunkId = new(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();

            if (chunkId == "fmt ")
            {
                audioFormat = reader.ReadInt16();
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // byte rate
                reader.ReadInt16(); // block align
                bitsPerSample = reader.ReadInt16();
                if (chunkSize > 16) inStream.Seek(chunkSize - 16, SeekOrigin.Current);
            }
            else if (chunkId == "data")
            {
                dataOffset = inStream.Position;
                dataSize = chunkSize;
                break;
            }
            else
            {
                inStream.Seek(chunkSize, SeekOrigin.Current);
            }
        }

        if (audioFormat != 1) throw new InvalidDataException($"Unsupported WAV format: {audioFormat}. Only PCM (1) is supported.");
        if (channels == 0 || sampleRate == 0 || bitsPerSample == 0)
            throw new InvalidDataException("Invalid WAV header: missing format information.");

        int bytesPerSample = bitsPerSample / 8;
        long totalSamples = dataSize / (bytesPerSample * channels);
        double recordDuration = options?.DataRecordDuration ?? 1.0;
        int samplesPerRecord = (int)(sampleRate * recordDuration);
        long totalRecords = (totalSamples + samplesPerRecord - 1) / samplesPerRecord;

        ReportProgress(progress, 20, "Writing EDF header...");

        double physMax = bitsPerSample switch { 8 => 127, 16 => 32767, 24 => 8388607, 32 => 2147483647.0, _ => 32767 };

        var headerData = new EdfHeaderData
        {
            PatientId = options?.PatientId ?? "X",
            RecordingId = options?.RecordingId ?? Path.GetFileName(inputPath),
            StartDateTime = options?.StartDateTime ?? File.GetCreationTime(inputPath),
            SignalCount = channels,
            DataRecordDuration = recordDuration,
            DataRecordCount = totalRecords,
        };

        for (int ch = 0; ch < channels; ch++)
        {
            headerData.Signals.Add(new SignalData
            {
                Label = channels == 1 ? "Audio" : $"Ch{ch + 1}",
                PhysicalDimension = "uV",
                PhysicalMin = -physMax,
                PhysicalMax = physMax,
                DigitalMin = -32768,
                DigitalMax = 32767,
                SamplesPerRecord = samplesPerRecord,
            });
        }

        await using var outStream = File.Create(outputPath);
        using var writer = new BinaryWriter(outStream);
        WriteEdfHeader(writer, headerData);

        ReportProgress(progress, 30, "Converting audio data...");
        inStream.Position = dataOffset;

        for (long rec = 0; rec < totalRecords; rec++)
        {
            ct.ThrowIfCancellationRequested();
            for (int ch = 0; ch < channels; ch++)
            {
                for (int s = 0; s < samplesPerRecord; s++)
                {
                    long sampleIdx = rec * samplesPerRecord + s;
                    int value = 0;
                    if (sampleIdx < totalSamples)
                    {
                        long pos = dataOffset + (sampleIdx * channels + ch) * bytesPerSample;
                        inStream.Position = pos;
                        value = bytesPerSample switch
                        {
                            1 => reader.ReadByte() - 128,
                            2 => reader.ReadInt16(),
                            3 => ReadBdfSample(reader),
                            4 => (int)(reader.ReadInt32() >> 16),
                            _ => 0
                        };
                    }
                    int digital = (int)Math.Clamp(value * 32767.0 / physMax, -32768, 32767);
                    WriteEdfSample(writer, digital);
                }
            }
            ReportProgress(progress, 30 + (int)(70.0 * rec / totalRecords), "Writing data records...");
        }

        ReportProgress(progress, 100, "Done");
    }
}

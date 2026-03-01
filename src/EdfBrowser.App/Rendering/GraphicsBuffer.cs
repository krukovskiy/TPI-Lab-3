namespace EdfBrowser.App.Rendering;

public readonly struct LineSegment
{
    public float X1 { get; init; }
    public float Y1 { get; init; }
    public float X2 { get; init; }
    public float Y2 { get; init; }
    public int SignalIndex { get; init; }
}

public sealed class GraphicsBuffer
{
    private readonly LineSegment[][] _partitions;
    private readonly int[] _partitionCounts;
    private readonly int _partitionCapacity;

    public GraphicsBuffer(int threadCount, int segmentsPerThread)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(threadCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segmentsPerThread);

        _partitionCapacity = segmentsPerThread;
        _partitions = new LineSegment[threadCount][];
        _partitionCounts = new int[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            _partitions[i] = new LineSegment[segmentsPerThread];
            _partitionCounts[i] = 0;
        }
    }

    public void AddSegment(int threadIndex, float x1, float y1, float x2, float y2, int signalIndex)
    {
        int count = _partitionCounts[threadIndex];
        if (count >= _partitionCapacity)
            return;

        _partitions[threadIndex][count] = new LineSegment
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            SignalIndex = signalIndex
        };
        _partitionCounts[threadIndex] = count + 1;
    }

    public void Clear()
    {
        for (int i = 0; i < _partitionCounts.Length; i++)
            _partitionCounts[i] = 0;
    }

    public int TotalSegmentCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < _partitionCounts.Length; i++)
                total += _partitionCounts[i];
            return total;
        }
    }

    public IEnumerable<LineSegment> GetAllSegments()
    {
        for (int p = 0; p < _partitions.Length; p++)
        {
            int count = _partitionCounts[p];
            var partition = _partitions[p];
            for (int i = 0; i < count; i++)
                yield return partition[i];
        }
    }

    /// <summary>
    /// Group by signal for batched color drawing.
    /// </summary>
    public Dictionary<int, List<LineSegment>> GetSegmentsBySignal()
    {
        var result = new Dictionary<int, List<LineSegment>>();

        for (int p = 0; p < _partitions.Length; p++)
        {
            int count = _partitionCounts[p];
            var partition = _partitions[p];

            for (int i = 0; i < count; i++)
            {
                ref readonly var seg = ref partition[i];
                if (!result.TryGetValue(seg.SignalIndex, out var list))
                {
                    list = new List<LineSegment>();
                    result[seg.SignalIndex] = list;
                }
                list.Add(seg);
            }
        }

        return result;
    }
}

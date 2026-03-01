using System.Globalization;

namespace EdfLib.Core;

/// <summary>
/// Represents a point in time or a duration at 100-nanosecond resolution,
/// matching the TIME_FIXP_SCALING (10 000 000) used in the original edflib.
/// </summary>
public readonly struct EdfTime : IEquatable<EdfTime>, IComparable<EdfTime>, IComparable
{
    /// <summary>Number of 100-ns ticks in one second.</summary>
    public const long TicksPerSecond = 10_000_000L;

    /// <summary>Number of 100-ns ticks in one millisecond.</summary>
    public const long TicksPerMillisecond = 10_000L;

    /// <summary>A zero-valued <see cref="EdfTime"/>.</summary>
    public static readonly EdfTime Zero = new(0L);

    /// <summary>The raw tick count (each tick = 100 ns).</summary>
    public long Ticks { get; }

    /// <summary>Creates an <see cref="EdfTime"/> from a raw tick count.</summary>
    public EdfTime(long ticks) => Ticks = ticks;

    // --- Factory methods ---

    /// <summary>Creates an <see cref="EdfTime"/> from a value in seconds.</summary>
    public static EdfTime FromSeconds(double seconds) =>
        new((long)(seconds * TicksPerSecond));

    /// <summary>Creates an <see cref="EdfTime"/> from a value in milliseconds.</summary>
    public static EdfTime FromMilliseconds(double milliseconds) =>
        new((long)(milliseconds * TicksPerMillisecond));

    /// <summary>Creates an <see cref="EdfTime"/> from a <see cref="TimeSpan"/>.</summary>
    public static EdfTime FromTimeSpan(TimeSpan timeSpan) =>
        new(timeSpan.Ticks);

    // --- Conversions ---

    /// <summary>Returns the total number of whole seconds.</summary>
    public double TotalSeconds => (double)Ticks / TicksPerSecond;

    /// <summary>Returns the total number of whole milliseconds.</summary>
    public double TotalMilliseconds => (double)Ticks / TicksPerMillisecond;

    /// <summary>Converts to an equivalent <see cref="TimeSpan"/>.</summary>
    public TimeSpan ToTimeSpan() => new(Ticks);

    // --- Arithmetic operators ---

    public static EdfTime operator +(EdfTime left, EdfTime right) =>
        new(left.Ticks + right.Ticks);

    public static EdfTime operator -(EdfTime left, EdfTime right) =>
        new(left.Ticks - right.Ticks);

    public static EdfTime operator -(EdfTime value) =>
        new(-value.Ticks);

    // --- Comparison operators ---

    public static bool operator ==(EdfTime left, EdfTime right) => left.Ticks == right.Ticks;
    public static bool operator !=(EdfTime left, EdfTime right) => left.Ticks != right.Ticks;
    public static bool operator <(EdfTime left, EdfTime right) => left.Ticks < right.Ticks;
    public static bool operator >(EdfTime left, EdfTime right) => left.Ticks > right.Ticks;
    public static bool operator <=(EdfTime left, EdfTime right) => left.Ticks <= right.Ticks;
    public static bool operator >=(EdfTime left, EdfTime right) => left.Ticks >= right.Ticks;

    // --- IEquatable<EdfTime> ---

    public bool Equals(EdfTime other) => Ticks == other.Ticks;

    public override bool Equals(object? obj) => obj is EdfTime other && Equals(other);

    public override int GetHashCode() => Ticks.GetHashCode();

    // --- IComparable<EdfTime> / IComparable ---

    public int CompareTo(EdfTime other) => Ticks.CompareTo(other.Ticks);

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is EdfTime other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(EdfTime)}.", nameof(obj));
    }

    // --- Formatting ---

    /// <summary>
    /// Returns a string representation in seconds with up to 7 fractional digits,
    /// e.g. "1.2345000" or "0.0000000".
    /// </summary>
    public override string ToString()
    {
        double seconds = TotalSeconds;
        return seconds.ToString("F7", CultureInfo.InvariantCulture);
    }
}

namespace EdfLib.Core;

/// <summary>
/// Represents a single EDF+/BDF+ annotation (replaces annotblck_t in the original C library).
/// </summary>
public sealed class EdfAnnotation : IEquatable<EdfAnnotation>
{
    /// <summary>Onset time of the annotation relative to recording start.</summary>
    public EdfTime Onset { get; }

    /// <summary>Duration of the annotation, or <c>null</c> if unspecified.</summary>
    public EdfTime? Duration { get; }

    /// <summary>
    /// Descriptive text of the annotation. Maximum length is
    /// <see cref="EdfConstants.MaxAnnotationLength"/> characters.
    /// </summary>
    public string Description { get; }

    public EdfAnnotation(EdfTime onset, EdfTime? duration, string description)
    {
        ArgumentNullException.ThrowIfNull(description);

        if (description.Length > EdfConstants.MaxAnnotationLength)
            throw new ArgumentException(
                $"Annotation description exceeds maximum length of {EdfConstants.MaxAnnotationLength} characters.",
                nameof(description));

        Onset = onset;
        Duration = duration;
        Description = description;
    }

    // --- Equality ---

    public bool Equals(EdfAnnotation? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Onset == other.Onset
            && Duration == other.Duration
            && Description == other.Description;
    }

    public override bool Equals(object? obj) => Equals(obj as EdfAnnotation);

    public override int GetHashCode() => HashCode.Combine(Onset, Duration, Description);

    public static bool operator ==(EdfAnnotation? left, EdfAnnotation? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(EdfAnnotation? left, EdfAnnotation? right) =>
        !(left == right);

    // --- Formatting ---

    public override string ToString()
    {
        string durationPart = Duration.HasValue ? $", Duration={Duration.Value}" : "";
        return $"Annotation(Onset={Onset}{durationPart}, \"{Description}\")";
    }
}

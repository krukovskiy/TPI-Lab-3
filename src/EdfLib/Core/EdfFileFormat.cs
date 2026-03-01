namespace EdfLib.Core;

/// <summary>
/// Identifies the specific variant of the European Data Format being used.
/// </summary>
public enum EdfFileFormat
{
    /// <summary>Standard EDF (16-bit samples).</summary>
    Edf,

    /// <summary>EDF+ with continuous or discontinuous recording (16-bit samples).</summary>
    EdfPlus,

    /// <summary>BioSemi Data Format (24-bit samples).</summary>
    Bdf,

    /// <summary>BDF+ with continuous or discontinuous recording (24-bit samples).</summary>
    BdfPlus
}

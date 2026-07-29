namespace Hase.Operator.Input;

/// <summary>
/// Identifies an expected failure while converting operator input into a
/// normalized Property value.
/// </summary>
public enum PropertyInputFailure
{
    None = 0,
    PropertyNotWritable = 1,
    MissingInput = 2,
    InvalidFormat = 3,
    ValueOutsideRange = 4,
    UnsupportedDataDescriptor = 5
}

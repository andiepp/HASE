namespace Hase.Operator.Input;

/// <summary>
/// Identifies an expected failure while converting operator input into one
/// normalized Command argument.
/// </summary>
public enum CommandArgumentInputFailure
{
    None = 0,
    MissingInput = 1,
    InvalidFormat = 2,
    ValueOutsideRange = 3,
    UnsupportedDataDescriptor = 4
}

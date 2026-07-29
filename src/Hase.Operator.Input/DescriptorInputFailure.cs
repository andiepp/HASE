namespace Hase.Operator.Input;

internal enum DescriptorInputFailure
{
    None = 0,
    MissingInput = 1,
    InvalidFormat = 2,
    ValueOutsideRange = 3,
    UnsupportedDataDescriptor = 4
}

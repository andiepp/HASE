namespace Hase.Protocol;

/// <summary>
/// Identifies the value type stored in a protocol variant.
/// Numeric values are part of the wire protocol and must never be changed
/// or reused.
/// </summary>
internal enum VariantType : byte
{
    Null = 0,
    Boolean = 1,
    Int32 = 2,
    Int64 = 3,
    Double = 4,
    String = 5
}

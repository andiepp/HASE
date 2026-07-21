namespace Hase.CompactProtocol;

/// <summary>
/// Reports the endpoint-side outcome of one compact property read.
/// </summary>
internal enum CompactPropertyReadStatus : byte
{
    Success =
        0x00,

    UnknownProperty =
        0x01,

    ReadFailed =
        0x02
}
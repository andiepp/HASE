namespace Hase.CompactProtocol;

/// <summary>
/// Reports the endpoint-side outcome of one compact property write.
/// </summary>
internal enum CompactPropertyWriteStatus : byte
{
    Success =
        0x00,

    UnknownProperty =
        0x01,

    WriteNotSupported =
        0x02,

    InvalidValue =
        0x03,

    WriteFailed =
        0x04
}
namespace Hase.CompactProtocol;

/// <summary>
/// Defines Compact Serial Protocol Version 1 message types.
/// </summary>
internal enum CompactSerialMessageType : byte
{
    BootstrapRequest =
        0x01,

    BootstrapResponse =
        0x02,

    ExecuteCommandRequest =
        0x03,

    ExecuteCommandResponse =
        0x04
}
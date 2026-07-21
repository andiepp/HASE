namespace Hase.CompactProtocol;

internal enum CompactSerialMessageType : byte
{
    BootstrapRequest =
        0x01,

    BootstrapResponse =
        0x02,

    ExecuteCommandRequest =
        0x03,

    ExecuteCommandResponse =
        0x04,

    ReadPropertyRequest =
        0x05,

    ReadPropertyResponse =
        0x06
}
namespace Hase.Protocol;

/// <summary>
/// Identifies the semantic type of a protocol message.
/// Numeric values are part of the wire protocol and must never be changed
/// or reused.
/// </summary>
public enum ProtocolMessageType : byte
{
    DiscoverRequest = 1,
    DiscoverResponse = 2,

    ReadPropertyRequest = 10,
    ReadPropertyResponse = 11,

    WritePropertyRequest = 20,
    WritePropertyResponse = 21,

    ExecuteCommandRequest = 30,
    ExecuteCommandResponse = 31,

    EventNotification = 40,

    // Values 50 and 51 are reserved for possible future
    // instrument-descriptor messages and must not be reused.

    ReadEndpointDescriptorRequest = 52,
    ReadEndpointDescriptorResponse = 53
}
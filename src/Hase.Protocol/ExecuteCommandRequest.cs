using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Protocol;

/// <summary>
/// Requests execution of a command.
/// </summary>
public sealed record ExecuteCommandRequest(
    CorrelationId CorrelationId,
    InstrumentId InstrumentId,
    DescriptorPath CommandPath,
    object? Argument)
    : ProtocolMessage(
        ProtocolVersion.Current,
        ProtocolMessageRole.Request,
        ProtocolMessageType.ExecuteCommandRequest,
        CorrelationId);
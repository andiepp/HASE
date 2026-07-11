using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Protocol;

/// <summary>
/// Reports that an event has occurred.
/// </summary>
public sealed record EventNotification(
    InstrumentId InstrumentId,
    DescriptorPath EventPath,
    DateTimeOffset TimestampUtc,
    object? Value)
    : ProtocolMessage(
        ProtocolVersion.Current,
        ProtocolMessageRole.Notification,
        ProtocolMessageType.EventNotification,
        CorrelationId.None);

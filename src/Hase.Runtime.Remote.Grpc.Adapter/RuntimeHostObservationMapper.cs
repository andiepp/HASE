using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized northbound runtime-host observations to version 1 stream
/// messages.
/// </summary>
public sealed class RuntimeHostObservationMapper
    : IRuntimeHostObservationMapper
{
    private readonly IRuntimeHostObservationKindMapper kindMapper;
    private readonly IRuntimeHostAttachmentObservationPayloadMapper
        attachmentPayloadMapper;
    private readonly
        IRuntimeHostConnectionStatusChangedObservationPayloadMapper
        connectionStatusPayloadMapper;
    private readonly IRuntimeHostPropertyValueChangedObservationPayloadMapper
        propertyValuePayloadMapper;
    private readonly IRuntimeHostEventOccurredObservationPayloadMapper
        eventPayloadMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public RuntimeHostObservationMapper(
        IRuntimeHostObservationKindMapper kindMapper,
        IRuntimeHostAttachmentObservationPayloadMapper attachmentPayloadMapper,
        IRuntimeHostConnectionStatusChangedObservationPayloadMapper
            connectionStatusPayloadMapper,
        IRuntimeHostPropertyValueChangedObservationPayloadMapper
            propertyValuePayloadMapper,
        IRuntimeHostEventOccurredObservationPayloadMapper eventPayloadMapper)
    {
        this.kindMapper =
            kindMapper
            ?? throw new ArgumentNullException(
                nameof(kindMapper));
        this.attachmentPayloadMapper =
            attachmentPayloadMapper
            ?? throw new ArgumentNullException(
                nameof(attachmentPayloadMapper));
        this.connectionStatusPayloadMapper =
            connectionStatusPayloadMapper
            ?? throw new ArgumentNullException(
                nameof(connectionStatusPayloadMapper));
        this.propertyValuePayloadMapper =
            propertyValuePayloadMapper
            ?? throw new ArgumentNullException(
                nameof(propertyValuePayloadMapper));
        this.eventPayloadMapper =
            eventPayloadMapper
            ?? throw new ArgumentNullException(
                nameof(eventPayloadMapper));
    }

    /// <inheritdoc />
    public GrpcV1.ObserveResponse Map(
        Northbound.RuntimeHostObservation observation)
    {
        ArgumentNullException.ThrowIfNull(
            observation);

        var mappedObservation =
            new GrpcV1.RuntimeHostObservation
            {
                Sequence =
                    checked(
                        (ulong)observation.Sequence.Value),
                EndpointId =
                    observation.EndpointId.Value,
                AttachmentGeneration =
                    observation.AttachmentGeneration.ToString(),
                Kind =
                    kindMapper.Map(
                        observation.Kind)
            };

        switch (observation.Payload)
        {
            case Northbound.RuntimeHostAttachmentPublishedObservationPayload
                payload:
                mappedObservation.AttachmentPublished =
                    attachmentPayloadMapper.Map(
                        payload)
                    ?? throw new InvalidOperationException(
                        "The attachment-published payload mapper returned null.");
                break;

            case Northbound.RuntimeHostAttachmentEndedObservationPayload
                payload:
                mappedObservation.AttachmentEnded =
                    attachmentPayloadMapper.Map(
                        payload)
                    ?? throw new InvalidOperationException(
                        "The attachment-ended payload mapper returned null.");
                break;

            case Northbound
                .RuntimeHostConnectionStatusChangedObservationPayload payload:
                mappedObservation.ConnectionStatusChanged =
                    connectionStatusPayloadMapper.Map(
                        payload)
                    ?? throw new InvalidOperationException(
                        "The connection-status payload mapper returned null.");
                break;

            case Northbound
                .RuntimeHostPropertyValueChangedObservationPayload payload:
                mappedObservation.PropertyValueChanged =
                    propertyValuePayloadMapper.Map(
                        payload)
                    ?? throw new InvalidOperationException(
                        "The Property-value payload mapper returned null.");
                break;

            case Northbound.RuntimeHostEventOccurredObservationPayload payload:
                mappedObservation.EventOccurred =
                    eventPayloadMapper.Map(
                        payload)
                    ?? throw new InvalidOperationException(
                        "The Event payload mapper returned null.");
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(observation),
                    observation.Payload.GetType(),
                    "The runtime-host observation payload is not supported.");
        }

        return new GrpcV1.ObserveResponse
        {
            Observation =
                mappedObservation
        };
    }
}

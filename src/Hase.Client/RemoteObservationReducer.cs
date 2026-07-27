using Hase.Core.Domain.Instruments;

namespace Hase.Client;

/// <summary>
/// Applies one authoritative initial snapshot and later strictly ordered
/// observations to immutable normalized client state.
/// </summary>
public sealed class RemoteObservationReducer
{
    /// <summary>
    /// Applies the mandatory initial snapshot to an empty stream state.
    /// </summary>
    public RemoteObservationState Initialize(
        RemoteObservationState state,
        RemoteObservationInitialSnapshot initialSnapshot)
    {
        ArgumentNullException.ThrowIfNull(
            state);
        ArgumentNullException.ThrowIfNull(
            initialSnapshot);

        if (state.IsInitialized)
        {
            throw new InvalidDataException(
                "The remote observation stream published more than one "
                + "initial snapshot.");
        }

        return new RemoteObservationState(
            initialSnapshot.Snapshot,
            initialSnapshot.SnapshotSequence,
            new Dictionary<
                RemotePropertyTarget,
                RemotePropertyValue>());
    }

    /// <summary>
    /// Applies one later strictly ordered observation.
    /// </summary>
    public RemoteObservationState Apply(
        RemoteObservationState state,
        RemoteRuntimeHostObservation observation)
    {
        ArgumentNullException.ThrowIfNull(
            state);
        ArgumentNullException.ThrowIfNull(
            observation);

        RemoteRuntimeHostSnapshot snapshot =
            state.Snapshot
            ?? throw new InvalidDataException(
                "The remote observation stream must begin with an initial "
                + "snapshot.");
        RemoteObservationSequence lastSequence =
            state.LastSequence
            ?? throw new InvalidDataException(
                "The initialized remote observation state has no sequence.");

        if (observation.Sequence.Value
            <= lastSequence.Value)
        {
            throw new InvalidDataException(
                "The remote observation sequence is not strictly "
                + "increasing.");
        }

        List<RemoteEndpointAttachmentSnapshot> attachments =
            snapshot.Attachments.ToList();
        var propertyValues =
            new Dictionary<
                RemotePropertyTarget,
                RemotePropertyValue>(
                state.PropertyValues);

        switch (observation.Payload)
        {
            case RemoteAttachmentPublishedObservationPayload payload:
                ApplyAttachmentPublished(
                    attachments,
                    payload);
                break;

            case RemoteAttachmentEndedObservationPayload:
                ApplyAttachmentEnded(
                    attachments,
                    propertyValues,
                    observation.Attachment);
                break;

            case RemoteConnectionStatusChangedObservationPayload payload:
                ApplyConnectionStatusChanged(
                    attachments,
                    observation.Attachment,
                    payload);
                break;

            case RemotePropertyValueChangedObservationPayload payload:
                ApplyPropertyValueChanged(
                    attachments,
                    propertyValues,
                    observation.Attachment,
                    payload);
                break;

            case RemoteEventOccurredObservationPayload payload:
                ValidateEventOccurred(
                    attachments,
                    observation.Attachment,
                    payload);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(observation),
                    observation.Payload.GetType(),
                    "The remote observation payload is not supported.");
        }

        return new RemoteObservationState(
            new RemoteRuntimeHostSnapshot(
                snapshot.RuntimeHostId,
                snapshot.ApiVersion,
                attachments),
            observation.Sequence,
            propertyValues);
    }

    private static void ApplyAttachmentPublished(
        ICollection<RemoteEndpointAttachmentSnapshot> attachments,
        RemoteAttachmentPublishedObservationPayload payload)
    {
        if (attachments.Any(
                existing =>
                    existing.EndpointId
                    == payload.Endpoint.EndpointId))
        {
            throw new InvalidDataException(
                "The remote observation published a duplicate current "
                + "endpoint attachment.");
        }

        attachments.Add(
            payload.Endpoint);
    }

    private static void ApplyAttachmentEnded(
        IList<RemoteEndpointAttachmentSnapshot> attachments,
        IDictionary<RemotePropertyTarget, RemotePropertyValue> propertyValues,
        RemoteEndpointAttachmentKey attachment)
    {
        int index =
            FindAttachmentIndex(
                attachments,
                attachment);

        attachments.RemoveAt(
            index);

        RemotePropertyTarget[] endedTargets =
            propertyValues.Keys
                .Where(
                    target =>
                        target.Attachment
                        == attachment)
                .ToArray();

        foreach (RemotePropertyTarget target
            in endedTargets)
        {
            propertyValues.Remove(
                target);
        }
    }

    private static void ApplyConnectionStatusChanged(
        IList<RemoteEndpointAttachmentSnapshot> attachments,
        RemoteEndpointAttachmentKey attachment,
        RemoteConnectionStatusChangedObservationPayload payload)
    {
        int index =
            FindAttachmentIndex(
                attachments,
                attachment);
        RemoteEndpointAttachmentSnapshot current =
            attachments[index];

        if (current.ConnectionStatus
            != payload.PreviousStatus)
        {
            throw new InvalidDataException(
                "The remote connection-status observation does not continue "
                + "from the current client state.");
        }

        attachments[index] =
            new RemoteEndpointAttachmentSnapshot(
                current.Generation,
                current.Descriptor,
                payload.CurrentStatus);
    }

    private static void ApplyPropertyValueChanged(
        IList<RemoteEndpointAttachmentSnapshot> attachments,
        IDictionary<RemotePropertyTarget, RemotePropertyValue> propertyValues,
        RemoteEndpointAttachmentKey attachment,
        RemotePropertyValueChangedObservationPayload payload)
    {
        RemoteEndpointAttachmentSnapshot endpoint =
            FindAttachment(
                attachments,
                attachment);
        InstrumentDescriptor instrument =
            FindInstrument(
                endpoint,
                payload.InstrumentId);

        if (!instrument.Interface.Properties.Any(
                property =>
                    property.Id
                    == payload.PropertyId))
        {
            throw new InvalidDataException(
                "The remote Property observation identifies a Property not "
                + "present in the published descriptor.");
        }

        var target =
            new RemotePropertyTarget(
                attachment,
                payload.InstrumentId,
                payload.PropertyId);

        if (propertyValues.TryGetValue(
                target,
                out RemotePropertyValue? currentValue)
            && payload.PreviousValue is not null
            && currentValue != payload.PreviousValue)
        {
            throw new InvalidDataException(
                "The remote Property observation previous value does not "
                + "match the current client state.");
        }

        propertyValues[target] =
            payload.CurrentValue;
    }

    private static void ValidateEventOccurred(
        IList<RemoteEndpointAttachmentSnapshot> attachments,
        RemoteEndpointAttachmentKey attachment,
        RemoteEventOccurredObservationPayload payload)
    {
        RemoteEndpointAttachmentSnapshot endpoint =
            FindAttachment(
                attachments,
                attachment);
        InstrumentDescriptor instrument =
            FindInstrument(
                endpoint,
                payload.InstrumentId);

        if (!instrument.Interface.Events.Any(
                eventDescriptor =>
                    eventDescriptor.Path
                    == payload.EventPath))
        {
            throw new InvalidDataException(
                "The remote Event observation identifies an Event not "
                + "present in the published descriptor.");
        }
    }

    private static int FindAttachmentIndex(
        IList<RemoteEndpointAttachmentSnapshot> attachments,
        RemoteEndpointAttachmentKey attachment)
    {
        for (int index = 0;
            index < attachments.Count;
            index++)
        {
            if (attachments[index].Key
                == attachment)
            {
                return index;
            }
        }

        throw new InvalidDataException(
            "The remote observation identifies an attachment that is not "
            + "current.");
    }

    private static RemoteEndpointAttachmentSnapshot FindAttachment(
        IList<RemoteEndpointAttachmentSnapshot> attachments,
        RemoteEndpointAttachmentKey attachment)
    {
        int index =
            FindAttachmentIndex(
                attachments,
                attachment);

        return attachments[index];
    }

    private static InstrumentDescriptor FindInstrument(
        RemoteEndpointAttachmentSnapshot endpoint,
        Hase.Core.Domain.Identity.InstrumentId instrumentId)
    {
        return endpoint.Descriptor.Instruments
            .SingleOrDefault(
                instrument =>
                    instrument.Id
                    == instrumentId)
            ?? throw new InvalidDataException(
                "The remote observation identifies an instrument not "
                + "present in the published descriptor.");
    }
}

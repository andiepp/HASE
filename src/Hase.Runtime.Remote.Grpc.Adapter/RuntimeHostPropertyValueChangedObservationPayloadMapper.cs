using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized Property-value-changed observation payloads to the version
/// 1 remote contract.
/// </summary>
public sealed class RuntimeHostPropertyValueChangedObservationPayloadMapper
    : IRuntimeHostPropertyValueChangedObservationPayloadMapper
{
    private readonly IPropertyValueMapper propertyValueMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public RuntimeHostPropertyValueChangedObservationPayloadMapper(
        IPropertyValueMapper propertyValueMapper)
    {
        this.propertyValueMapper =
            propertyValueMapper
            ?? throw new ArgumentNullException(
                nameof(propertyValueMapper));
    }

    /// <inheritdoc />
    public GrpcV1.PropertyValueChangedObservation Map(
        Northbound.RuntimeHostPropertyValueChangedObservationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(
            payload);

        var result =
            new GrpcV1.PropertyValueChangedObservation
            {
                InstrumentId =
                    payload.InstrumentId.Value,
                PropertyId =
                    payload.PropertyId.Value
            };

        if (payload.PreviousValue is not null)
        {
            result.PreviousValue =
                propertyValueMapper.Map(
                    payload.PreviousValue)
                ?? throw new InvalidOperationException(
                    "The previous Property value mapper returned null.");
        }

        result.CurrentValue =
            propertyValueMapper.Map(
                payload.CurrentValue)
            ?? throw new InvalidOperationException(
                "The current Property value mapper returned null.");

        return result;
    }
}

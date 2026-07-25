using Google.Protobuf.WellKnownTypes;
using CoreProperties = global::Hase.Core.Domain.Properties;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps runtime Property values to the version 1 remote contract.
/// </summary>
public sealed class PropertyValueMapper
    : IPropertyValueMapper
{
    private readonly IRemoteValueMapper valueMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public PropertyValueMapper(
        IRemoteValueMapper valueMapper)
    {
        this.valueMapper =
            valueMapper
            ?? throw new ArgumentNullException(
                nameof(valueMapper));
    }

    /// <inheritdoc />
    public GrpcV1.PropertyValue Map(
        CoreProperties.PropertyValue source)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        var result =
            new GrpcV1.PropertyValue
            {
                TimestampUtc =
                    Timestamp.FromDateTimeOffset(
                        source.TimestampUtc),
                Quality =
                    MapQuality(
                        source.Quality)
            };

        if (source.Value is not null)
        {
            result.Value =
                valueMapper.Map(
                    source.Value)
                ?? throw new InvalidOperationException(
                    "The remote value mapper returned null.");
        }

        return result;
    }

    private static GrpcV1.PropertyQuality MapQuality(
        CoreProperties.PropertyQuality quality)
    {
        return quality switch
        {
            CoreProperties.PropertyQuality.Good =>
                GrpcV1.PropertyQuality.Good,
            CoreProperties.PropertyQuality.Uncertain =>
                GrpcV1.PropertyQuality.Uncertain,
            CoreProperties.PropertyQuality.Bad =>
                GrpcV1.PropertyQuality.Bad,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(quality),
                    quality,
                    "The Property quality is not supported.")
        };
    }
}

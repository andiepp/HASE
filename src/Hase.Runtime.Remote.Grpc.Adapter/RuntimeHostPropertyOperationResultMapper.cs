using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps authoritative northbound Property operation results to the version 1
/// remote contract.
/// </summary>
public sealed class RuntimeHostPropertyOperationResultMapper
    : IRuntimeHostPropertyOperationResultMapper
{
    private readonly IRuntimeHostPropertyOperationStatusMapper statusMapper;
    private readonly IPropertyValueMapper propertyValueMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public RuntimeHostPropertyOperationResultMapper(
        IRuntimeHostPropertyOperationStatusMapper statusMapper,
        IPropertyValueMapper propertyValueMapper)
    {
        this.statusMapper =
            statusMapper
            ?? throw new ArgumentNullException(
                nameof(statusMapper));

        this.propertyValueMapper =
            propertyValueMapper
            ?? throw new ArgumentNullException(
                nameof(propertyValueMapper));
    }

    /// <inheritdoc />
    public GrpcV1.PropertyOperationResult Map(
        Northbound.RuntimeHostPropertyOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        var mappedResult =
            new GrpcV1.PropertyOperationResult
            {
                Status =
                    statusMapper.Map(
                        result.Status)
            };

        if (result.ConfirmedValue is not null)
        {
            mappedResult.ConfirmedValue =
                propertyValueMapper.Map(
                    result.ConfirmedValue)
                ?? throw new InvalidOperationException(
                    "The Property value mapper returned null.");
        }

        if (result.Diagnostic is not null)
        {
            mappedResult.Diagnostic =
                result.Diagnostic;
        }

        return mappedResult;
    }
}

using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized northbound Command operation results to the version 1
/// remote contract.
/// </summary>
public sealed class RuntimeHostCommandOperationResultMapper
    : IRuntimeHostCommandOperationResultMapper
{
    private readonly IRuntimeHostCommandOperationStatusMapper statusMapper;
    private readonly IRemoteValueMapper remoteValueMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public RuntimeHostCommandOperationResultMapper(
        IRuntimeHostCommandOperationStatusMapper statusMapper,
        IRemoteValueMapper remoteValueMapper)
    {
        this.statusMapper =
            statusMapper
            ?? throw new ArgumentNullException(
                nameof(statusMapper));

        this.remoteValueMapper =
            remoteValueMapper
            ?? throw new ArgumentNullException(
                nameof(remoteValueMapper));
    }

    /// <inheritdoc />
    public GrpcV1.CommandOperationResult Map(
        Northbound.RuntimeHostCommandOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        var mappedResult =
            new GrpcV1.CommandOperationResult
            {
                Status =
                    statusMapper.Map(
                        result.Status)
            };

        if (result.ReturnValue is not null)
        {
            mappedResult.ReturnValue =
                remoteValueMapper.Map(
                    result.ReturnValue)
                ?? throw new InvalidOperationException(
                    "The remote value mapper returned null.");
        }

        if (result.Diagnostic is not null)
        {
            mappedResult.Diagnostic =
                result.Diagnostic;
        }

        return mappedResult;
    }
}

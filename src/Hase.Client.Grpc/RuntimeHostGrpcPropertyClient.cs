using Grpc.Core;
using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc;

internal interface IRuntimeHostGrpcPropertyClient
{
    Task<GrpcV1.PropertyOperationResult> ReadPropertyAsync(
        GrpcV1.ReadAuthoritativePropertyRequest request,
        CancellationToken cancellationToken);
}

internal sealed class RuntimeHostGrpcPropertyClient
    : IRuntimeHostGrpcPropertyClient
{
    private readonly GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiClient
        client;

    public RuntimeHostGrpcPropertyClient(
        GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiClient client)
    {
        this.client =
            client
            ?? throw new ArgumentNullException(
                nameof(client));
    }

    public async Task<GrpcV1.PropertyOperationResult> ReadPropertyAsync(
        GrpcV1.ReadAuthoritativePropertyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        return await client.ReadAuthoritativePropertyAsync(
                request,
                cancellationToken:
                    cancellationToken)
            .ResponseAsync
            .ConfigureAwait(
                false);
    }
}

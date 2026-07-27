using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc;

internal interface IRuntimeHostGrpcCommandClient
{
    Task<GrpcV1.CommandOperationResult> ExecuteAsync(
        GrpcV1.ExecuteCommandRequest request,
        CancellationToken cancellationToken);
}

internal sealed class RuntimeHostGrpcCommandClient
    : IRuntimeHostGrpcCommandClient
{
    private readonly GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiClient
        client;

    public RuntimeHostGrpcCommandClient(
        GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiClient client)
    {
        this.client =
            client
            ?? throw new ArgumentNullException(
                nameof(client));
    }

    public async Task<GrpcV1.CommandOperationResult> ExecuteAsync(
        GrpcV1.ExecuteCommandRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        return await client.ExecuteCommandAsync(
                request,
                cancellationToken:
                    cancellationToken)
            .ResponseAsync
            .ConfigureAwait(
                false);
    }
}

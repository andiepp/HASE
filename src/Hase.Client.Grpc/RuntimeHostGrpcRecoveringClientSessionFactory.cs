using Hase.Runtime.Remote.Grpc.Hosting;

namespace Hase.Client.Grpc;

/// <summary>
/// Loads one external ADR-0032 client configuration and composes a normalized
/// recovering runtime-host client session without connecting it.
/// </summary>
public sealed class RuntimeHostGrpcRecoveringClientSessionFactory
    : IRuntimeHostClientSessionFactory
{
    private readonly Func<
        string,
        CancellationToken,
        Task<RuntimeHostPrivateNetworkClientOptions>> loadOptionsAsync;
    private readonly Func<
        RuntimeHostPrivateNetworkClientOptions,
        IRuntimeHostClientSession> createSession;

    public RuntimeHostGrpcRecoveringClientSessionFactory()
        : this(
            RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync,
            options =>
                new RuntimeHostGrpcRecoveringClientSession(
                    options))
    {
    }

    internal RuntimeHostGrpcRecoveringClientSessionFactory(
        Func<
            string,
            CancellationToken,
            Task<RuntimeHostPrivateNetworkClientOptions>> loadOptionsAsync,
        Func<
            RuntimeHostPrivateNetworkClientOptions,
            IRuntimeHostClientSession> createSession)
    {
        this.loadOptionsAsync =
            loadOptionsAsync
            ?? throw new ArgumentNullException(
                nameof(loadOptionsAsync));
        this.createSession =
            createSession
            ?? throw new ArgumentNullException(
                nameof(createSession));
    }

    /// <summary>
    /// Loads and validates the specified external configuration and creates an
    /// unconnected normalized client session.
    /// </summary>
    public async Task<IRuntimeHostClientSession> CreateAsync(
        string configurationFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            configurationFilePath);

        RuntimeHostPrivateNetworkClientOptions options =
            await loadOptionsAsync(
                    configurationFilePath,
                    cancellationToken)
                .ConfigureAwait(
                    false);

        cancellationToken.ThrowIfCancellationRequested();

        return createSession(
                options)
            ?? throw new InvalidOperationException(
                "The runtime-host client session factory returned null.");
    }
}

using Hase.Runtime.Remote.Grpc.Hosting;
using Hase.Client.Diagnostics;

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
    private readonly ClientDiagnosticPublisher diagnostics;

    public RuntimeHostGrpcRecoveringClientSessionFactory()
        : this(
            RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync,
            options =>
                new RuntimeHostGrpcRecoveringClientSession(
                    options),
            new ClientDiagnosticPublisher())
    {
    }

    public RuntimeHostGrpcRecoveringClientSessionFactory(
        ClientDiagnosticPublisher diagnostics)
        : this(
            RuntimeHostPrivateNetworkClientOptionsFile.LoadAsync,
            options =>
                new RuntimeHostGrpcRecoveringClientSession(
                    options),
            diagnostics)
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
        : this(
            loadOptionsAsync,
            createSession,
            new ClientDiagnosticPublisher())
    {
    }

    internal RuntimeHostGrpcRecoveringClientSessionFactory(
        Func<
            string,
            CancellationToken,
            Task<RuntimeHostPrivateNetworkClientOptions>> loadOptionsAsync,
        Func<
            RuntimeHostPrivateNetworkClientOptions,
            IRuntimeHostClientSession> createSession,
        ClientDiagnosticPublisher diagnostics)
    {
        this.loadOptionsAsync =
            loadOptionsAsync
            ?? throw new ArgumentNullException(
                nameof(loadOptionsAsync));
        this.createSession =
            createSession
            ?? throw new ArgumentNullException(
                nameof(createSession));
        this.diagnostics =
            diagnostics
            ?? throw new ArgumentNullException(
                nameof(diagnostics));
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

        Guid operationId = Guid.NewGuid();
        var duration = System.Diagnostics.Stopwatch.StartNew();
        PublishConfiguration(
            "ConfigurationLoadStarted",
            operationId);

        RuntimeHostPrivateNetworkClientOptions options;
        try
        {
            options =
                await loadOptionsAsync(
                        configurationFilePath,
                        cancellationToken)
                    .ConfigureAwait(
                        false);
        }
        catch (Exception exception)
        {
            duration.Stop();
            PublishConfiguration(
                "ConfigurationLoadFailed",
                operationId,
                duration.Elapsed,
                exception is OperationCanceledException
                    ? ClientDiagnosticOutcome.Cancelled
                    : ClientDiagnosticOutcome.Failed,
                exception is OperationCanceledException
                    ? ClientDiagnosticSeverity.Information
                    : ClientDiagnosticSeverity.Error);
            throw;
        }

        IRuntimeHostClientSession session;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            session =
                createSession(options)
                ?? throw new InvalidOperationException(
                    "The runtime-host client session factory returned null.");
        }
        catch (Exception exception)
        {
            duration.Stop();
            PublishConfiguration(
                "ConfigurationLoadFailed",
                operationId,
                duration.Elapsed,
                exception is OperationCanceledException
                    ? ClientDiagnosticOutcome.Cancelled
                    : ClientDiagnosticOutcome.Failed,
                exception is OperationCanceledException
                    ? ClientDiagnosticSeverity.Information
                    : ClientDiagnosticSeverity.Error);
            throw;
        }

        duration.Stop();
        PublishConfiguration(
            "ConfigurationLoadCompleted",
            operationId,
            duration.Elapsed,
            ClientDiagnosticOutcome.Succeeded);

        return diagnostics.IsEnabled(ClientDiagnosticLevel.Operational)
            ? new DiagnosticRuntimeHostClientSession(session, diagnostics)
            : session;
    }

    private void PublishConfiguration(
        string eventName,
        Guid operationId,
        TimeSpan? duration = null,
        ClientDiagnosticOutcome? outcome = null,
        ClientDiagnosticSeverity severity = ClientDiagnosticSeverity.Information)
    {
        diagnostics.Publish(
            ClientDiagnosticLevel.Operational,
            () => new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Operational,
                ClientDiagnosticCategory.ClientConfiguration,
                eventName,
                severity,
                operationId: operationId,
                duration: duration,
                outcome: outcome));
    }
}

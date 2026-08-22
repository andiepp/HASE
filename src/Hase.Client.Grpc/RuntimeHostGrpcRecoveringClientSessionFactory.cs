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
    private readonly Func<
        string,
        CancellationToken,
        Task<bool>> probeDevelopmentAsync;
    private readonly Func<
        string,
        CancellationToken,
        Task<RuntimeHostDevelopmentLoopbackClientOptions>>
        loadDevelopmentOptionsAsync;
    private readonly Func<
        RuntimeHostDevelopmentLoopbackClientOptions,
        IRuntimeHostClientSession> createDevelopmentSession;
    private readonly ClientDiagnosticPublisher diagnostics;

    public RuntimeHostGrpcRecoveringClientSessionFactory()
        : this(
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
            RuntimeHostClientConfigurationDocument.IsDevelopmentLoopbackAsync,
            RuntimeHostDevelopmentLoopbackClientOptionsFile.LoadAsync,
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
        : this(
            loadOptionsAsync,
            createSession,
            (_, _) =>
                Task.FromResult(
                    false),
            (_, _) =>
                throw new InvalidOperationException(
                    "The injected session factory does not support the "
                    + "development loopback profile."),
            _ =>
                throw new InvalidOperationException(
                    "The injected session factory does not support the "
                    + "development loopback profile."),
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
            IRuntimeHostClientSession> createSession,
        Func<
            string,
            CancellationToken,
            Task<bool>> probeDevelopmentAsync,
        Func<
            string,
            CancellationToken,
            Task<RuntimeHostDevelopmentLoopbackClientOptions>>
            loadDevelopmentOptionsAsync,
        Func<
            RuntimeHostDevelopmentLoopbackClientOptions,
            IRuntimeHostClientSession> createDevelopmentSession,
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
        this.probeDevelopmentAsync =
            probeDevelopmentAsync
            ?? throw new ArgumentNullException(
                nameof(probeDevelopmentAsync));
        this.loadDevelopmentOptionsAsync =
            loadDevelopmentOptionsAsync
            ?? throw new ArgumentNullException(
                nameof(loadDevelopmentOptionsAsync));
        this.createDevelopmentSession =
            createDevelopmentSession
            ?? throw new ArgumentNullException(
                nameof(createDevelopmentSession));
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

        bool isDevelopmentLoopback;
        RuntimeHostPrivateNetworkClientOptions? options = null;
        RuntimeHostDevelopmentLoopbackClientOptions? developmentOptions = null;
        try
        {
            isDevelopmentLoopback =
                await probeDevelopmentAsync(
                        configurationFilePath,
                        cancellationToken)
                    .ConfigureAwait(
                        false);

            if (isDevelopmentLoopback)
            {
                developmentOptions =
                    await loadDevelopmentOptionsAsync(
                            configurationFilePath,
                            cancellationToken)
                        .ConfigureAwait(
                            false);
            }
            else
            {
                options =
                    await loadOptionsAsync(
                            configurationFilePath,
                            cancellationToken)
                        .ConfigureAwait(
                            false);
            }
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
                (isDevelopmentLoopback
                    ? createDevelopmentSession(
                        developmentOptions!)
                    : createSession(
                        options!))
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

        if (isDevelopmentLoopback)
        {
            PublishDevelopmentLoopbackActive(
                operationId);
        }

        return diagnostics.IsEnabled(ClientDiagnosticLevel.Operational)
            ? new DiagnosticRuntimeHostClientSession(session, diagnostics)
            : session;
    }

    private void PublishDevelopmentLoopbackActive(
        Guid operationId)
    {
        diagnostics.Publish(
            ClientDiagnosticLevel.Operational,
            () => new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Operational,
                ClientDiagnosticCategory.ClientConfiguration,
                "DevelopmentLoopbackConfigurationActive",
                ClientDiagnosticSeverity.Warning,
                operationId: operationId,
                outcome: ClientDiagnosticOutcome.Succeeded,
                metadata: new Dictionary<string, string>(
                    StringComparer.Ordinal)
                {
                    ["Profile"] = "DevelopmentLoopback",
                    ["Security"] =
                        "None - loopback only, no TLS, "
                        + "no client certificates"
                }));
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

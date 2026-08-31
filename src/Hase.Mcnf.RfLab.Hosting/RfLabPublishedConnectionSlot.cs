using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;

namespace Hase.Mcnf.RfLab.Hosting;

internal sealed class RfLabPublishedConnectionSlot
    : IEndpointAttachmentPropertyOperations,
      IEndpointAttachmentCommandOperations,
      IAsyncDisposable
{
    private readonly RuntimeEndpoint runtimeEndpoint;
    private readonly RfLabOperationalConnectionFactory connectionFactory;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private RfLabOperationalConnection? connection;
    private bool disposed;

    public RfLabPublishedConnectionSlot(
        RfLabOperationalConnection connection,
        RfLabOperationalConnectionFactory connectionFactory,
        TimeProvider timeProvider)
    {
        this.connection = connection
            ?? throw new ArgumentNullException(nameof(connection));
        this.connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
        this.timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
        runtimeEndpoint = connection.RuntimeEndpoint;
    }

    public RuntimeEndpoint RuntimeEndpoint => runtimeEndpoint;

    internal async Task ProbeHealthAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (disposed || connection is null)
            {
                throw new InvalidOperationException(
                    "The RF-Lab attachment does not own an active connection.");
            }

            try
            {
                await connection.ProbeHealthAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                ProjectHealthFault();
                throw;
            }
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrumentId);
        ArgumentNullException.ThrowIfNull(propertyId);
        cancellationToken.ThrowIfCancellationRequested();

        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (disposed || connection is null)
            {
                return Unavailable();
            }

            return await connection.PropertyOperations
                .ReadAsync(instrumentId, propertyId, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        object? requestedValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrumentId);
        ArgumentNullException.ThrowIfNull(propertyId);
        cancellationToken.ThrowIfCancellationRequested();

        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (disposed || connection is null)
            {
                return Unavailable();
            }

            return await connection.PropertyOperations
                .WriteAsync(
                    instrumentId,
                    propertyId,
                    requestedValue,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<EndpointAttachmentCommandOperationResult> ExecuteAsync(
        InstrumentId instrumentId,
        DescriptorPath commandPath,
        object? argument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrumentId);
        ArgumentNullException.ThrowIfNull(commandPath);
        cancellationToken.ThrowIfCancellationRequested();

        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (disposed || connection is null)
            {
                return CommandUnavailable();
            }

            return await connection.CommandOperations
                .ExecuteAsync(
                    instrumentId,
                    commandPath,
                    argument,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task ReplaceAsync(
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serialOptions);
        cancellationToken.ThrowIfCancellationRequested();

        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (runtimeEndpoint.ConnectionStatus.State != EndpointConnectionState.Faulted)
            {
                throw new InvalidOperationException(
                    "RF-Lab connection replacement requires a faulted endpoint.");
            }

            RfLabOperationalConnection? previous = connection;
            connection = null;
            if (previous is not null)
            {
                try
                {
                    await previous.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    ProjectFault();
                    throw;
                }
            }

            runtimeEndpoint.UpdateConnectionStatus(
                new EndpointConnectionStatus(
                    EndpointConnectionState.Reconnecting,
                    timeProvider.GetUtcNow()));

            try
            {
                RfLabOperationalConnection replacement = await connectionFactory
                    .OpenForEndpointAsync(runtimeEndpoint, serialOptions, cancellationToken)
                    .ConfigureAwait(false);
                connection = replacement;
                runtimeEndpoint.UpdateConnectionStatus(
                    new EndpointConnectionStatus(
                        EndpointConnectionState.Ready,
                        timeProvider.GetUtcNow()));
            }
            catch
            {
                ProjectFault();
                throw;
            }
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            RfLabOperationalConnection? ownedConnection = connection;
            connection = null;
            if (ownedConnection is not null)
            {
                await ownedConnection.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            operationGate.Release();
        }
    }

    private void ProjectFault()
    {
        runtimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Faulted,
                timeProvider.GetUtcNow(),
                "The RF-Lab connection replacement failed."));
    }

    private void ProjectHealthFault()
    {
        if (runtimeEndpoint.ConnectionStatus.State == EndpointConnectionState.Faulted)
        {
            return;
        }

        runtimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Faulted,
                timeProvider.GetUtcNow(),
                "The RF-Lab passive health probe failed."));
    }

    private static EndpointAttachmentPropertyOperationResult Unavailable() =>
        EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.Unavailable,
            "The RF-Lab attachment does not own an active connection.");

    private static EndpointAttachmentCommandOperationResult CommandUnavailable() =>
        EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.Unavailable,
            "The RF-Lab attachment does not own an active connection.");
}

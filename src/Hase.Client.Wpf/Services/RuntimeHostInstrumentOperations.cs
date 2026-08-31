using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Wpf.Services;

/// <summary>
/// Binds normalized instrument operations to one attachment and instrument,
/// routing each operation through the client session the workspace already
/// uses.
/// </summary>
public sealed class RuntimeHostInstrumentOperations
    : IRuntimeHostInstrumentOperations
{
    private readonly InstrumentId instrumentId;
    private readonly Func<
        RemotePropertyTarget,
        CancellationToken,
        Task<RemotePropertyOperationResult>> readAsync;
    private readonly Func<
        RemotePropertyTarget,
        RemoteValue,
        CancellationToken,
        Task<RemotePropertyOperationResult>> writeAsync;
    private readonly Func<
        RemoteCommandExecutionRequest,
        CancellationToken,
        Task<RemoteCommandOperationResult>> executeAsync;

    public RuntimeHostInstrumentOperations(
        RemoteEndpointAttachmentKey attachment,
        InstrumentId instrumentId,
        Func<
            RemotePropertyTarget,
            CancellationToken,
            Task<RemotePropertyOperationResult>> readAsync,
        Func<
            RemotePropertyTarget,
            RemoteValue,
            CancellationToken,
            Task<RemotePropertyOperationResult>> writeAsync,
        Func<
            RemoteCommandExecutionRequest,
            CancellationToken,
            Task<RemoteCommandOperationResult>> executeAsync)
    {
        Attachment = attachment
            ?? throw new ArgumentNullException(nameof(attachment));
        this.instrumentId = instrumentId
            ?? throw new ArgumentNullException(nameof(instrumentId));
        this.readAsync = readAsync
            ?? throw new ArgumentNullException(nameof(readAsync));
        this.writeAsync = writeAsync
            ?? throw new ArgumentNullException(nameof(writeAsync));
        this.executeAsync = executeAsync
            ?? throw new ArgumentNullException(nameof(executeAsync));
    }

    /// <inheritdoc />
    public RemoteEndpointAttachmentKey Attachment { get; }

    /// <inheritdoc />
    public Task<RemotePropertyOperationResult> ReadAsync(
        string propertyId,
        CancellationToken cancellationToken = default) =>
        readAsync(
            CreatePropertyTarget(propertyId),
            cancellationToken);

    /// <inheritdoc />
    public Task<RemotePropertyOperationResult> WriteAsync(
        string propertyId,
        RemoteValue requestedValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestedValue);

        return writeAsync(
            CreatePropertyTarget(propertyId),
            requestedValue,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RemoteCommandOperationResult> ExecuteAsync(
        string commandPath,
        RemoteValue? argument = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandPath);

        return executeAsync(
            new RemoteCommandExecutionRequest(
                new RemoteCommandTarget(
                    Attachment,
                    instrumentId,
                    DescriptorPath.Parse(commandPath)),
                argument),
            cancellationToken);
    }

    private RemotePropertyTarget CreatePropertyTarget(string propertyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyId);

        return new RemotePropertyTarget(
            Attachment,
            instrumentId,
            new PropertyId(propertyId));
    }
}

using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Adapts Compact Serial Protocol Command execution to the
/// transport-independent attachment operation port.
/// </summary>
internal sealed class CompactEndpointAttachmentCommandOperations
    : IEndpointAttachmentCommandOperations
{
    private readonly CompactCommandMap _commandMap;

    private readonly Func<
        byte,
        CancellationToken,
        Task<CompactCommandExecutionStatus>>
        _executeAsync;

    internal CompactEndpointAttachmentCommandOperations(
        CompactRuntimeEndpointConnectionCoordinator coordinator,
        CompactCommandMap commandMap)
        : this(
            commandMap,
            (coordinator
                ?? throw new ArgumentNullException(
                    nameof(coordinator)))
                .ExecuteCommandAsync)
    {
    }

    internal CompactEndpointAttachmentCommandOperations(
        CompactCommandMap commandMap,
        Func<
            byte,
            CancellationToken,
            Task<CompactCommandExecutionStatus>>
            executeAsync)
    {
        _commandMap =
            commandMap
            ?? throw new ArgumentNullException(
                nameof(commandMap));

        _executeAsync =
            executeAsync
            ?? throw new ArgumentNullException(
                nameof(executeAsync));
    }

    /// <inheritdoc />
    public async Task<EndpointAttachmentCommandOperationResult> ExecuteAsync(
        InstrumentId instrumentId,
        DescriptorPath commandPath,
        object? argument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            instrumentId);

        ArgumentNullException.ThrowIfNull(
            commandPath);

        cancellationToken.ThrowIfCancellationRequested();

        CompactCommandMapping? mapping =
            _commandMap.Find(
                instrumentId,
                commandPath);

        if (mapping is null)
        {
            return EndpointAttachmentCommandOperationResult.Failed(
                EndpointAttachmentCommandOperationStatus.Failure,
                "The logical Command does not have a compact wire mapping.");
        }

        if (argument is not null)
        {
            return EndpointAttachmentCommandOperationResult.Failed(
                EndpointAttachmentCommandOperationStatus
                    .ArgumentNotSupported);
        }

        try
        {
            CompactCommandExecutionStatus status =
                await _executeAsync(
                    mapping.CompactCommandId,
                    cancellationToken);

            return status switch
            {
                CompactCommandExecutionStatus.Success =>
                    EndpointAttachmentCommandOperationResult.Successful(),

                CompactCommandExecutionStatus.UnknownCommand =>
                    EndpointAttachmentCommandOperationResult.Failed(
                        EndpointAttachmentCommandOperationStatus.Failure,
                        "The compact endpoint reported an unknown Command."),

                CompactCommandExecutionStatus.ExecutionFailed =>
                    EndpointAttachmentCommandOperationResult.Failed(
                        EndpointAttachmentCommandOperationStatus.Failure,
                        "The compact endpoint reported Command execution "
                        + "failure."),

                _ =>
                    EndpointAttachmentCommandOperationResult.Failed(
                        EndpointAttachmentCommandOperationStatus.Failure)
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return EndpointAttachmentCommandOperationResult.Failed(
                EndpointAttachmentCommandOperationStatus.TimedOut,
                "The compact endpoint Command operation timed out.");
        }
        catch (InvalidDataException)
        {
            return EndpointAttachmentCommandOperationResult.Failed(
                EndpointAttachmentCommandOperationStatus.Failure);
        }
        catch (InvalidOperationException)
        {
            return CreateUnavailableResult();
        }
        catch (IOException)
        {
            return CreateUnavailableResult();
        }
    }

    private static EndpointAttachmentCommandOperationResult
        CreateUnavailableResult()
    {
        return EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.Unavailable,
            "The compact attachment cannot currently perform the "
            + "Command operation.");
    }
}
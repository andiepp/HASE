using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi;
using Hase.Scpi.Kel103.Runtime;

namespace Hase.Scpi.Kel103.Hosting;

public sealed class Kel103EndpointAttachmentCommandOperations
    : IEndpointAttachmentCommandOperations
{
    private readonly Func<InstrumentId, DescriptorPath, object?, CancellationToken, Task<RuntimeCommand>>
        executeAsync;
    private readonly Func<bool> isSessionFaulted;
    private readonly RuntimeEndpoint? runtimeEndpoint;
    private readonly TimeProvider timeProvider;

    public Kel103EndpointAttachmentCommandOperations(
        Kel103RuntimeEndpointAdapter runtimeAdapter,
        TimeProvider? timeProvider = null)
        : this(
            (runtimeAdapter ?? throw new ArgumentNullException(nameof(runtimeAdapter))).ExecuteAsync,
            () => runtimeAdapter.IsFaulted,
            runtimeAdapter.RuntimeEndpoint,
            timeProvider ?? TimeProvider.System)
    {
    }

    internal Kel103EndpointAttachmentCommandOperations(
        Func<InstrumentId, DescriptorPath, object?, CancellationToken, Task<RuntimeCommand>> executeAsync,
        Func<bool> isSessionFaulted,
        RuntimeEndpoint? runtimeEndpoint,
        TimeProvider timeProvider)
    {
        this.executeAsync = executeAsync
            ?? throw new ArgumentNullException(nameof(executeAsync));
        this.isSessionFaulted = isSessionFaulted
            ?? throw new ArgumentNullException(nameof(isSessionFaulted));
        this.runtimeEndpoint = runtimeEndpoint;
        this.timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
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
        CommandCategory category = Classify(commandPath);

        try
        {
            await executeAsync(
                instrumentId,
                commandPath,
                argument,
                cancellationToken).ConfigureAwait(false);
            return EndpointAttachmentCommandOperationResult.Successful();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ProjectSessionFault();
            throw;
        }
        catch (KeyNotFoundException)
        {
            return Failure();
        }
        catch (ArgumentException)
        {
            return ArgumentNotSupported();
        }
        catch (TimeoutException)
        {
            ProjectSessionFault();
            return TimedOut(category);
        }
        catch (InvalidDataException)
        {
            ProjectSessionFault();
            return Failure();
        }
        catch (InvalidOperationException)
        {
            if (!isSessionFaulted())
            {
                return Rejected(category);
            }

            ProjectSessionFault();
            return Unavailable(category);
        }
        catch (Kel103MutationOutcomeUncertainException)
        {
            ProjectSessionFault();
            return Uncertain(category);
        }
        catch (ScpiCommandTransmissionException exception)
            when (exception.ExecutionMayHaveOccurred)
        {
            ProjectSessionFault();
            return Uncertain(category);
        }
        catch (IOException)
        {
            ProjectSessionFault();
            return Unavailable(category);
        }
    }

    private static EndpointAttachmentCommandOperationResult ArgumentNotSupported() =>
        EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.ArgumentNotSupported);

    private static EndpointAttachmentCommandOperationResult Rejected(
        CommandCategory category) =>
        EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.Rejected,
            category switch
            {
                CommandCategory.ModeSelection =>
                    "KEL-103 mode selection requires authoritative input OFF.",
                CommandCategory.InputActivation =>
                    "Generic KEL-103 input activation rejects SHORT mode.",
                CommandCategory.ShortCircuitActivation =>
                    "KEL-103 SHORT activation requires authoritative input OFF and SHORT mode.",
                CommandCategory.InputDeactivation =>
                    "The KEL-103 input-deactivation operation was rejected.",
                _ => "The KEL-103 Command was rejected."
            });

    private static EndpointAttachmentCommandOperationResult Failure() =>
        EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.Failure);

    private static EndpointAttachmentCommandOperationResult TimedOut(
        CommandCategory category) =>
        EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.TimedOut,
            $"The KEL-103 {OperationName(category)} timed out.");

    private static EndpointAttachmentCommandOperationResult Unavailable(
        CommandCategory category) =>
        EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.Unavailable,
            $"The KEL-103 attachment cannot currently perform {OperationName(category)}.");

    private static EndpointAttachmentCommandOperationResult Uncertain(
        CommandCategory category) =>
        EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.Unavailable,
            category == CommandCategory.ModeSelection
                ? "The KEL-103 mode-selection outcome is uncertain. Physically verify input and operating mode before continuing."
                : $"The KEL-103 {OperationName(category)} outcome is uncertain. Physically verify input state before continuing.");

    private static CommandCategory Classify(DescriptorPath commandPath)
    {
        if (Kel103ModeSelectionMapping.All.Any(
                mapping => mapping.CommandPath == commandPath))
        {
            return CommandCategory.ModeSelection;
        }

        if (commandPath == Kel103InputControlMapping.Activate.CommandPath)
        {
            return CommandCategory.InputActivation;
        }

        if (commandPath == Kel103InputControlMapping.Deactivate.CommandPath)
        {
            return CommandCategory.InputDeactivation;
        }

        return commandPath == Kel103InputControlMapping.ShortCircuitActivate.CommandPath
            ? CommandCategory.ShortCircuitActivation
            : CommandCategory.Unknown;
    }

    private static string OperationName(CommandCategory category) =>
        category switch
        {
            CommandCategory.ModeSelection => "mode-selection operation",
            CommandCategory.InputActivation => "input-activation operation",
            CommandCategory.InputDeactivation => "input-deactivation operation",
            CommandCategory.ShortCircuitActivation => "confirmed SHORT-activation operation",
            _ => "Command operation"
        };

    private enum CommandCategory
    {
        Unknown,
        ModeSelection,
        InputActivation,
        InputDeactivation,
        ShortCircuitActivation
    }

    private void ProjectSessionFault()
    {
        if (runtimeEndpoint is null
            || !isSessionFaulted()
            || runtimeEndpoint.ConnectionStatus.State == EndpointConnectionState.Faulted)
        {
            return;
        }

        runtimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Faulted,
                timeProvider.GetUtcNow(),
                "The KEL-103 communication session is faulted."));
    }
}

namespace Hase.Runtime.Diagnostics;

/// <summary>
/// Publishes one correlated operational start and terminal diagnostic pair.
/// </summary>
public sealed class RuntimeDiagnosticOperation
{
    private readonly RuntimeDiagnosticPublisher diagnostics;
    private readonly RuntimeDiagnosticCategory category;
    private readonly string completedEventName;
    private readonly string failedEventName;
    private readonly string? endpointId;
    private readonly Guid? attachmentGeneration;
    private readonly RuntimeDiagnosticDirection? direction;
    private readonly IReadOnlyDictionary<string, string> details;
    private readonly TimeProvider timeProvider;
    private readonly long startedTimestamp;
    private int completed;

    public RuntimeDiagnosticOperation(
        RuntimeDiagnosticPublisher diagnostics,
        RuntimeDiagnosticCategory category,
        string startedEventName,
        string completedEventName,
        string failedEventName,
        string? endpointId = null,
        Guid? attachmentGeneration = null,
        RuntimeDiagnosticDirection? direction = null,
        IReadOnlyDictionary<string, string>? details = null)
        : this(
            diagnostics,
            category,
            startedEventName,
            completedEventName,
            failedEventName,
            endpointId,
            attachmentGeneration,
            direction,
            details,
            TimeProvider.System)
    {
    }

    public RuntimeDiagnosticOperation(
        RuntimeDiagnosticPublisher diagnostics,
        RuntimeDiagnosticCategory category,
        string startedEventName,
        string completedEventName,
        string failedEventName,
        string? endpointId,
        Guid? attachmentGeneration,
        RuntimeDiagnosticDirection? direction,
        IReadOnlyDictionary<string, string>? details,
        TimeProvider timeProvider)
    {
        this.diagnostics =
            diagnostics ??
            throw new ArgumentNullException(
                nameof(diagnostics));

        ArgumentNullException.ThrowIfNull(
            timeProvider);

        ValidateEventName(
            completedEventName,
            nameof(completedEventName));

        ValidateEventName(
            failedEventName,
            nameof(failedEventName));

        RuntimeDiagnosticEvent startedEvent =
            new(
                RuntimeDiagnosticLevel.Operational,
                category,
                startedEventName,
                endpointId: endpointId,
                attachmentGeneration: attachmentGeneration,
                direction: direction,
                operationId: Guid.NewGuid(),
                details: details);

        this.category = category;
        this.completedEventName = completedEventName.Trim();
        this.failedEventName = failedEventName.Trim();
        this.endpointId = startedEvent.EndpointId;
        this.attachmentGeneration = attachmentGeneration;
        this.direction = direction;
        this.details = startedEvent.Details;
        this.timeProvider = timeProvider;
        OperationId = startedEvent.OperationId!.Value;
        startedTimestamp = timeProvider.GetTimestamp();

        diagnostics.Publish(
            startedEvent);
    }

    public Guid OperationId { get; }

    public void Complete(
        RuntimeDiagnosticOutcome outcome)
    {
        if (!Enum.IsDefined(
                outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "Value is not defined.");
        }

        if (Interlocked.CompareExchange(
                ref completed,
                1,
                0) != 0)
        {
            return;
        }

        bool succeeded =
            outcome ==
            RuntimeDiagnosticOutcome.Succeeded;

        diagnostics.Publish(
            new RuntimeDiagnosticEvent(
                RuntimeDiagnosticLevel.Operational,
                category,
                succeeded
                    ? completedEventName
                    : failedEventName,
                succeeded
                    ? RuntimeDiagnosticSeverity.Information
                    : RuntimeDiagnosticSeverity.Warning,
                endpointId,
                attachmentGeneration,
                direction,
                OperationId,
                timeProvider.GetElapsedTime(
                    startedTimestamp),
                outcome,
                details));
    }

    public async Task RunAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            operation);

        try
        {
            await operation(
                    cancellationToken)
                .ConfigureAwait(
                    false);

            Complete(
                RuntimeDiagnosticOutcome.Succeeded);
        }
        catch (TimeoutException)
        {
            Complete(
                RuntimeDiagnosticOutcome.TimedOut);

            throw;
        }
        catch (OperationCanceledException)
        {
            Complete(
                RuntimeDiagnosticOutcome.Cancelled);

            throw;
        }
        catch
        {
            Complete(
                RuntimeDiagnosticOutcome.Failed);

            throw;
        }
    }

    public async Task<TResult> RunAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            operation);

        try
        {
            TResult result =
                await operation(
                        cancellationToken)
                    .ConfigureAwait(
                        false);

            Complete(
                RuntimeDiagnosticOutcome.Succeeded);

            return result;
        }
        catch (TimeoutException)
        {
            Complete(
                RuntimeDiagnosticOutcome.TimedOut);

            throw;
        }
        catch (OperationCanceledException)
        {
            Complete(
                RuntimeDiagnosticOutcome.Cancelled);

            throw;
        }
        catch
        {
            Complete(
                RuntimeDiagnosticOutcome.Failed);

            throw;
        }
    }

    public async Task<TResult> RunAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        Func<TResult, RuntimeDiagnosticOutcome> outcomeSelector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            operation);

        ArgumentNullException.ThrowIfNull(
            outcomeSelector);

        try
        {
            TResult result =
                await operation(
                        cancellationToken)
                    .ConfigureAwait(
                        false);

            RuntimeDiagnosticOutcome outcome =
                SelectOutcome(
                    result,
                    outcomeSelector);

            Complete(
                outcome);

            return result;
        }
        catch (TimeoutException)
        {
            Complete(
                RuntimeDiagnosticOutcome.TimedOut);

            throw;
        }
        catch (OperationCanceledException)
        {
            Complete(
                RuntimeDiagnosticOutcome.Cancelled);

            throw;
        }
        catch
        {
            Complete(
                RuntimeDiagnosticOutcome.Failed);

            throw;
        }
    }

    private static RuntimeDiagnosticOutcome SelectOutcome<TResult>(
        TResult result,
        Func<TResult, RuntimeDiagnosticOutcome> outcomeSelector)
    {
        try
        {
            RuntimeDiagnosticOutcome outcome =
                outcomeSelector(
                    result);

            return Enum.IsDefined(
                       outcome)
                ? outcome
                : RuntimeDiagnosticOutcome.Failed;
        }
        catch
        {
            return RuntimeDiagnosticOutcome.Failed;
        }
    }

    private static void ValidateEventName(
        string eventName,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                eventName))
        {
            throw new ArgumentException(
                "Event name must not be empty.",
                parameterName);
        }
    }
}

namespace Hase.Client;

/// <summary>
/// Represents one immutable normalized request to execute a remote Command.
/// </summary>
/// <remarks>
/// A request value does not imply permission to retry it. Remote Command
/// execution must never be retried automatically after timeout, cancellation,
/// transport loss, or another ambiguous outcome.
/// </remarks>
public sealed record RemoteCommandExecutionRequest
{
    /// <summary>
    /// Initializes one normalized remote Command-execution request.
    /// </summary>
    public RemoteCommandExecutionRequest(
        RemoteCommandTarget target,
        RemoteValue? argument = null)
    {
        Target =
            target
            ?? throw new ArgumentNullException(
                nameof(target));

        Argument =
            argument;
    }

    /// <summary>
    /// Gets the generation-scoped Command target.
    /// </summary>
    public RemoteCommandTarget Target
    {
        get;
    }

    /// <summary>
    /// Gets the optional normalized Command argument.
    /// </summary>
    public RemoteValue? Argument
    {
        get;
    }
}

namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents the result of atomically creating persisted runtime-host
/// identity when it is missing.
/// </summary>
public sealed record RuntimeHostIdentityStoreCreateResult
{
    /// <summary>
    /// Initializes a runtime-host identity store creation result.
    /// </summary>
    public RuntimeHostIdentityStoreCreateResult(
        RuntimeHostId runtimeHostId,
        RuntimeHostIdentityStoreCreateOutcome outcome)
    {
        RuntimeHostId =
            runtimeHostId
            ?? throw new ArgumentNullException(
                nameof(runtimeHostId));

        if (!Enum.IsDefined(
                outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "The runtime-host identity store creation outcome is not defined.");
        }

        Outcome =
            outcome;
    }

    /// <summary>
    /// Gets the authoritative persisted runtime-host identity.
    /// </summary>
    public RuntimeHostId RuntimeHostId
    {
        get;
    }

    /// <summary>
    /// Gets the store creation outcome.
    /// </summary>
    public RuntimeHostIdentityStoreCreateOutcome Outcome
    {
        get;
    }
}
namespace Hase.Runtime.Northbound;

/// <summary>
/// Indicates that a subscription could not accept an observation without
/// exceeding its bounded buffer.
/// </summary>
public sealed class RuntimeHostObservationGapException
    : Exception
{
    public RuntimeHostObservationGapException()
        : base(
            "The observation subscription exceeded its bounded buffer. "
            + "Open a new subscription and recover current state from its "
            + "initial snapshot.")
    {
    }
}
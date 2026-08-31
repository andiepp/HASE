namespace Hase.Mcnf.RfLab.Runtime;

/// <summary>
/// Reports that an RF-Lab mutation was transmitted but its acknowledged
/// response was not established; the node may or may not have executed it.
/// The mutation is never retried or replayed.
/// </summary>
public sealed class RfLabMutationOutcomeUncertainException : Exception
{
    public RfLabMutationOutcomeUncertainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

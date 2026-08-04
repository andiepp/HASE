namespace Hase.Scpi.Kel103.Runtime;

public sealed class Kel103MutationOutcomeUncertainException : IOException
{
    public Kel103MutationOutcomeUncertainException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }

    public bool ExecutionMayHaveOccurred => true;
}

namespace Hase.Scpi;

public sealed class ScpiCommandTransmissionException : IOException
{
    public ScpiCommandTransmissionException(
        string message,
        bool executionMayHaveOccurred,
        Exception innerException)
        : base(message, innerException)
    {
        ExecutionMayHaveOccurred = executionMayHaveOccurred;
    }

    public bool ExecutionMayHaveOccurred { get; }
}

namespace Hase.CompactProtocol;

internal sealed class CompactBootstrapIdentityException : IOException
{
    public CompactBootstrapIdentityException(
        Exception innerException)
        : base(
            "The compact bootstrap response contains invalid identity data.",
            innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);
    }
}
namespace Hase.CompactProtocol;

internal sealed class CompactProtocolVersionNotSupportedException : IOException
{
    public CompactProtocolVersionNotSupportedException(
        byte actualVersion,
        byte supportedVersion)
        : base(
            $"Compact protocol version {actualVersion} is not supported. " +
            $"The supported version is {supportedVersion}.")
    {
        ActualVersion = actualVersion;
        SupportedVersion = supportedVersion;
    }

    public byte ActualVersion { get; }

    public byte SupportedVersion { get; }
}
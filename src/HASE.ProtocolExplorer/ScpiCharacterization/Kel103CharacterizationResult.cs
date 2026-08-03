namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed class Kel103CharacterizationResult
{
    private readonly byte[] _responseBytes;

    public Kel103CharacterizationResult(
        IReadOnlyList<byte> responseBytes,
        Kel103ResponseTerminator responseTerminator,
        bool commandEchoDetected,
        TimeSpan timeToFirstByte,
        TimeSpan totalDuration,
        string productIdentity,
        string firmware,
        bool identityVerified)
    {
        ArgumentNullException.ThrowIfNull(
            responseBytes);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            productIdentity);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            firmware);

        if (!Enum.IsDefined(
                responseTerminator))
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseTerminator),
                responseTerminator,
                "The KEL-103 response terminator is not supported.");
        }

        if (timeToFirstByte < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeToFirstByte),
                timeToFirstByte,
                "The time to first byte cannot be negative.");
        }

        if (totalDuration < timeToFirstByte)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalDuration),
                totalDuration,
                "The total duration cannot be shorter than the time to first byte.");
        }

        _responseBytes =
            responseBytes.ToArray();

        ResponseTerminator =
            responseTerminator;

        CommandEchoDetected =
            commandEchoDetected;

        TimeToFirstByte =
            timeToFirstByte;

        TotalDuration =
            totalDuration;

        ProductIdentity =
            productIdentity;

        Firmware =
            firmware;

        IdentityVerified =
            identityVerified;
    }

    public IReadOnlyList<byte> ResponseBytes =>
        Array.AsReadOnly(
            _responseBytes);

    public int ResponseByteCount =>
        _responseBytes.Length;

    public Kel103ResponseTerminator ResponseTerminator
    {
        get;
    }

    public bool CommandEchoDetected
    {
        get;
    }

    public TimeSpan TimeToFirstByte
    {
        get;
    }

    public TimeSpan TotalDuration
    {
        get;
    }

    public string ProductIdentity
    {
        get;
    }

    public string Firmware
    {
        get;
    }

    public bool IdentityVerified
    {
        get;
    }

    public string SanitizedIdentity =>
        $"{ProductIdentity} {Firmware} SN:<redacted>";
}


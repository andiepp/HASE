namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed class Kel103CharacterizationOptions
{
    public static readonly TimeSpan DefaultTotalResponseTimeout =
        TimeSpan.FromSeconds(
            3);

    public static readonly TimeSpan DefaultPostFirstByteIdleInterval =
        TimeSpan.FromMilliseconds(
            200);

    public const int DefaultMaximumResponseBytes =
        512;

    public Kel103CharacterizationOptions(
        Kel103CommandTerminator commandTerminator)
        : this(
            commandTerminator,
            DefaultTotalResponseTimeout,
            DefaultPostFirstByteIdleInterval,
            DefaultMaximumResponseBytes)
    {
    }

    public Kel103CharacterizationOptions(
        Kel103CommandTerminator commandTerminator,
        TimeSpan totalResponseTimeout,
        TimeSpan postFirstByteIdleInterval,
        int maximumResponseBytes)
    {
        if (!Enum.IsDefined(
                commandTerminator))
        {
            throw new ArgumentOutOfRangeException(
                nameof(commandTerminator),
                commandTerminator,
                "The KEL-103 command terminator is not supported.");
        }

        if (totalResponseTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalResponseTimeout),
                totalResponseTimeout,
                "The total response timeout must be positive.");
        }

        if (postFirstByteIdleInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(postFirstByteIdleInterval),
                postFirstByteIdleInterval,
                "The post-first-byte idle interval must be positive.");
        }

        if (postFirstByteIdleInterval >= totalResponseTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(postFirstByteIdleInterval),
                postFirstByteIdleInterval,
                "The post-first-byte idle interval must be shorter than the total response timeout.");
        }

        if (maximumResponseBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResponseBytes),
                maximumResponseBytes,
                "The maximum response size must be positive.");
        }

        CommandTerminator =
            commandTerminator;

        TotalResponseTimeout =
            totalResponseTimeout;

        PostFirstByteIdleInterval =
            postFirstByteIdleInterval;

        MaximumResponseBytes =
            maximumResponseBytes;
    }

    public Kel103CommandTerminator CommandTerminator
    {
        get;
    }

    public TimeSpan TotalResponseTimeout
    {
        get;
    }

    public TimeSpan PostFirstByteIdleInterval
    {
        get;
    }

    public int MaximumResponseBytes
    {
        get;
    }
}


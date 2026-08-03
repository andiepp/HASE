namespace Hase.Scpi;

public sealed class ScpiTextFramingOptions
{
    public ScpiTextFramingOptions(
        ScpiCommandTerminator commandTerminator,
        ScpiResponseTerminator responseTerminator,
        TimeSpan totalExchangeTimeout,
        int maximumResponseBytes)
    {
        if (!Enum.IsDefined(commandTerminator))
        {
            throw new ArgumentOutOfRangeException(nameof(commandTerminator));
        }

        if (!Enum.IsDefined(responseTerminator))
        {
            throw new ArgumentOutOfRangeException(nameof(responseTerminator));
        }

        if (totalExchangeTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalExchangeTimeout));
        }

        if (maximumResponseBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResponseBytes));
        }

        CommandTerminator = commandTerminator;
        ResponseTerminator = responseTerminator;
        TotalExchangeTimeout = totalExchangeTimeout;
        MaximumResponseBytes = maximumResponseBytes;
    }

    public ScpiCommandTerminator CommandTerminator { get; }

    public ScpiResponseTerminator ResponseTerminator { get; }

    public TimeSpan TotalExchangeTimeout { get; }

    public int MaximumResponseBytes { get; }
}

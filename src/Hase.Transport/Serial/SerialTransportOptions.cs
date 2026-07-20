namespace Hase.Transport.Serial;

/// <summary>
/// Defines the operating-system serial-port target and communication settings
/// used by a serial transport connection.
/// </summary>
/// <remarks>
/// The port name and communication settings describe reachability only. They
/// are not authoritative HASE endpoint identity or USB-adapter identity.
/// </remarks>
public sealed class SerialTransportOptions
{
    /// <summary>
    /// Initializes serial transport options using eight data bits, no parity,
    /// one stop bit, and no flow control.
    /// </summary>
    public SerialTransportOptions(
        string portName,
        int baudRate)
        : this(
            portName,
            baudRate,
            dataBits: 8,
            SerialParity.None,
            SerialStopBits.One,
            SerialHandshake.None)
    {
    }

    /// <summary>
    /// Initializes serial transport options.
    /// </summary>
    public SerialTransportOptions(
        string portName,
        int baudRate,
        int dataBits,
        SerialParity parity,
        SerialStopBits stopBits,
        SerialHandshake handshake)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            portName);

        if (baudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baudRate),
                baudRate,
                "The serial baud rate must be positive.");
        }

        if (dataBits is < 5 or > 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dataBits),
                dataBits,
                "The serial data-bit count must be between 5 and 8.");
        }

        if (!Enum.IsDefined(
                parity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(parity),
                parity,
                "The serial parity value is not supported.");
        }

        if (!Enum.IsDefined(
                stopBits))
        {
            throw new ArgumentOutOfRangeException(
                nameof(stopBits),
                stopBits,
                "The serial stop-bit value is not supported.");
        }

        if (!Enum.IsDefined(
                handshake))
        {
            throw new ArgumentOutOfRangeException(
                nameof(handshake),
                handshake,
                "The serial handshake value is not supported.");
        }

        PortName =
            portName;

        BaudRate =
            baudRate;

        DataBits =
            dataBits;

        Parity =
            parity;

        StopBits =
            stopBits;

        Handshake =
            handshake;
    }

    /// <summary>
    /// Gets the operating-system port name or device path.
    /// </summary>
    public string PortName
    {
        get;
    }

    /// <summary>
    /// Gets the serial baud rate.
    /// </summary>
    public int BaudRate
    {
        get;
    }

    /// <summary>
    /// Gets the number of data bits in each serial character.
    /// </summary>
    public int DataBits
    {
        get;
    }

    /// <summary>
    /// Gets the parity mode.
    /// </summary>
    public SerialParity Parity
    {
        get;
    }

    /// <summary>
    /// Gets the stop-bit mode.
    /// </summary>
    public SerialStopBits StopBits
    {
        get;
    }

    /// <summary>
    /// Gets the flow-control mode.
    /// </summary>
    public SerialHandshake Handshake
    {
        get;
    }
}
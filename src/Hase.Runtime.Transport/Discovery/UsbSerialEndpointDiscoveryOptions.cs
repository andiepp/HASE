using Hase.Transport.Serial;

namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Defines the shared serial settings and verification timeout used while
/// discovering USB serial compact endpoints.
/// </summary>
/// <remarks>
/// These options do not identify a serial port or a HASE endpoint.
/// Each candidate supplies its own operating-system port name, and Compact
/// Serial Protocol bootstrap supplies authoritative endpoint identity.
/// </remarks>
public sealed class UsbSerialEndpointDiscoveryOptions
{
    /// <summary>
    /// Initializes discovery options using eight data bits, no parity,
    /// one stop bit, and no flow control.
    /// </summary>
    public UsbSerialEndpointDiscoveryOptions(
        int baudRate,
        TimeSpan verificationTimeout)
        : this(
            baudRate,
            dataBits: 8,
            SerialParity.None,
            SerialStopBits.One,
            SerialHandshake.None,
            verificationTimeout)
    {
    }

    /// <summary>
    /// Initializes discovery options using explicit serial settings.
    /// </summary>
    public UsbSerialEndpointDiscoveryOptions(
        int baudRate,
        int dataBits,
        SerialParity parity,
        SerialStopBits stopBits,
        SerialHandshake handshake,
        TimeSpan verificationTimeout)
    {
        var validatedOptions =
            new SerialTransportOptions(
                portName: "VALIDATION",
                baudRate,
                dataBits,
                parity,
                stopBits,
                handshake);

        if (verificationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(verificationTimeout),
                verificationTimeout,
                "The candidate verification timeout must be positive.");
        }

        BaudRate =
            validatedOptions.BaudRate;

        DataBits =
            validatedOptions.DataBits;

        Parity =
            validatedOptions.Parity;

        StopBits =
            validatedOptions.StopBits;

        Handshake =
            validatedOptions.Handshake;

        VerificationTimeout =
            verificationTimeout;
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

    /// <summary>
    /// Gets the maximum duration of one candidate verification.
    /// </summary>
    public TimeSpan VerificationTimeout
    {
        get;
    }

    internal SerialTransportOptions CreateTransportOptions(
        string portName)
    {
        return new SerialTransportOptions(
            portName,
            BaudRate,
            DataBits,
            Parity,
            StopBits,
            Handshake);
    }
}
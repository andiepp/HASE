namespace Hase.Transport.Serial;

/// <summary>
/// Classifies a failure that occurred specifically while opening an
/// operating-system serial port.
/// </summary>
public enum SerialPortOpenFailure
{
    /// <summary>
    /// The port is already owned by another process or connection.
    /// </summary>
    Busy = 0,

    /// <summary>
    /// The configured port is missing, disconnected, or otherwise no
    /// longer available.
    /// </summary>
    Unavailable = 1,

    /// <summary>
    /// The operating system denied access to the port.
    /// </summary>
    AccessDenied = 2,

    /// <summary>
    /// The port could not be opened for another reason.
    /// </summary>
    Failed = 3
}

/// <summary>
/// Reports a semantically classified failure that occurred while opening
/// an operating-system serial port.
/// </summary>
public sealed class SerialPortOpenException
    : IOException
{
    /// <summary>
    /// Initializes a serial-port open exception.
    /// </summary>
    public SerialPortOpenException(
        string portName,
        SerialPortOpenFailure failure,
        Exception innerException)
        : base(
            CreateMessage(
                portName,
                failure),
            innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            portName);

        if (!Enum.IsDefined(
            failure))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failure),
                failure,
                "The serial-port open failure classification is invalid.");
        }

        ArgumentNullException.ThrowIfNull(
            innerException);

        PortName =
            portName;

        Failure =
            failure;
    }

    /// <summary>
    /// Gets the operating-system serial port name or device path.
    /// </summary>
    public string PortName
    {
        get;
    }

    /// <summary>
    /// Gets the classified serial-port open failure.
    /// </summary>
    public SerialPortOpenFailure Failure
    {
        get;
    }

    private static string CreateMessage(
        string portName,
        SerialPortOpenFailure failure)
    {
        return $"Serial port '{portName}' could not be opened: {failure}.";
    }
}
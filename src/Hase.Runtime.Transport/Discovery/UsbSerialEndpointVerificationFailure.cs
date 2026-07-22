namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Describes why a USB serial candidate could not be verified as a
/// compatible HASE compact endpoint.
/// </summary>
public enum UsbSerialEndpointVerificationFailure
{
    /// <summary>
    /// The serial port is already owned by another process or connection.
    /// </summary>
    PortBusy = 0,

    /// <summary>
    /// The enumerated serial port is no longer available.
    /// </summary>
    PortUnavailable = 1,

    /// <summary>
    /// The operating system denied access to the serial port.
    /// </summary>
    AccessDenied = 2,

    /// <summary>
    /// The serial connection could not be established for another reason.
    /// </summary>
    ConnectionFailed = 3,

    /// <summary>
    /// Candidate verification did not complete within the configured
    /// timeout.
    /// </summary>
    TimedOut = 4,

    /// <summary>
    /// The connected serial device did not behave as a HASE compact
    /// endpoint.
    /// </summary>
    NonHaseEndpoint = 5,

    /// <summary>
    /// The device returned a malformed, unexpected, or otherwise invalid
    /// compact response.
    /// </summary>
    InvalidCompactResponse = 6,

    /// <summary>
    /// The endpoint reported an unsupported compact protocol version.
    /// </summary>
    UnsupportedCompactProtocolVersion = 7,

    /// <summary>
    /// The endpoint returned a missing or invalid authoritative identity.
    /// </summary>
    InvalidEndpointIdentity = 8,

    /// <summary>
    /// The endpoint reported a descriptor reference that is not present
    /// in the host repository.
    /// </summary>
    UnknownDescriptorReference = 9,

    /// <summary>
    /// The resolved descriptor is incompatible with the compact endpoint
    /// profile or bootstrap information.
    /// </summary>
    IncompatibleDescriptor = 10
}
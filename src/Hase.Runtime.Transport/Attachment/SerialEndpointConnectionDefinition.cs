using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Serial;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Describes how a runtime host can reach a resource-constrained endpoint
/// through a serial connection.
/// </summary>
/// <remarks>
/// The serial port and communication settings describe reachability only.
/// They are not authoritative HASE endpoint identity or USB-adapter identity.
/// </remarks>
public sealed class SerialEndpointConnectionDefinition
    : IEndpointConnectionDefinition
{
    private SerialEndpointConnectionDefinition(
        SerialTransportOptions transportOptions,
        EndpointConnectionOrigin origin,
        EndpointId? expectedEndpointId)
    {
        TransportOptions =
            transportOptions;

        Origin =
            origin;

        ExpectedEndpointId =
            expectedEndpointId;
    }

    /// <summary>
    /// Gets the serial connection target and communication settings.
    /// </summary>
    public SerialTransportOptions TransportOptions
    {
        get;
    }

    /// <inheritdoc />
    public EndpointConnectionOrigin Origin
    {
        get;
    }

    /// <inheritdoc />
    public EndpointId? ExpectedEndpointId
    {
        get;
    }

    /// <summary>
    /// Creates a connection definition from an endpoint already verified
    /// during USB serial discovery.
    /// </summary>
    /// <remarks>
    /// The candidate port and discovery options provide reachability. The
    /// endpoint identity returned by authoritative compact bootstrap becomes
    /// the expected identity for attachment-time revalidation.
    /// </remarks>
    public static SerialEndpointConnectionDefinition
        FromVerifiedEndpoint(
            VerifiedUsbSerialEndpoint verifiedEndpoint,
            UsbSerialEndpointDiscoveryOptions discoveryOptions)
    {
        ArgumentNullException.ThrowIfNull(
            verifiedEndpoint);

        ArgumentNullException.ThrowIfNull(
            discoveryOptions);

        SerialTransportOptions transportOptions =
            discoveryOptions.CreateTransportOptions(
                verifiedEndpoint.Candidate.PortName);

        return new SerialEndpointConnectionDefinition(
            transportOptions,
            EndpointConnectionOrigin.Discovered,
            verifiedEndpoint.EndpointId);
    }

    /// <summary>
    /// Creates an explicitly configured serial connection definition.
    /// </summary>
    public static SerialEndpointConnectionDefinition
        FromConfiguration(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId = null)
    {
        ArgumentNullException.ThrowIfNull(
            transportOptions);

        return new SerialEndpointConnectionDefinition(
            transportOptions,
            EndpointConnectionOrigin.Configured,
            expectedEndpointId);
    }
}
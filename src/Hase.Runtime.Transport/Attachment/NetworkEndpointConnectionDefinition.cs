using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Tcp;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Describes how a runtime host can reach a native HASE endpoint
/// through framed TCP.
/// </summary>
/// <remarks>
/// The TCP host and port describe reachability only. They are not
/// authoritative HASE endpoint identity.
/// </remarks>
public sealed class NetworkEndpointConnectionDefinition
    : IEndpointConnectionDefinition
{
    private NetworkEndpointConnectionDefinition(
        TcpTransportOptions transportOptions,
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
    /// Gets the framed-TCP connection target.
    /// </summary>
    public TcpTransportOptions TransportOptions
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
    /// during network discovery.
    /// </summary>
    public static NetworkEndpointConnectionDefinition
        FromVerifiedEndpoint(
            VerifiedNetworkEndpoint verifiedEndpoint)
    {
        ArgumentNullException.ThrowIfNull(
            verifiedEndpoint);

        var transportOptions =
            new TcpTransportOptions(
                verifiedEndpoint
                    .Candidate
                    .Address
                    .ToString(),
                verifiedEndpoint
                    .Candidate
                    .Port);

        return new NetworkEndpointConnectionDefinition(
            transportOptions,
            EndpointConnectionOrigin.Discovered,
            verifiedEndpoint.EndpointId);
    }

    /// <summary>
    /// Creates an explicitly configured network connection definition.
    /// </summary>
    public static NetworkEndpointConnectionDefinition
        FromConfiguration(
            TcpTransportOptions transportOptions,
            EndpointId? expectedEndpointId = null)
    {
        ArgumentNullException.ThrowIfNull(
            transportOptions);

        return new NetworkEndpointConnectionDefinition(
            transportOptions,
            EndpointConnectionOrigin.Configured,
            expectedEndpointId);
    }
}
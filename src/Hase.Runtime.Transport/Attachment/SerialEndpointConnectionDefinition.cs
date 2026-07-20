using Hase.Core.Domain.Identity;
using Hase.Transport.Serial;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Describes how a runtime host can reach a resource-constrained endpoint
/// through a manually configured serial connection.
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
        EndpointId? expectedEndpointId)
    {
        TransportOptions =
            transportOptions;

        ExpectedEndpointId =
            expectedEndpointId;
    }

    /// <summary>
    /// Gets the configured serial connection target.
    /// </summary>
    public SerialTransportOptions TransportOptions
    {
        get;
    }

    /// <inheritdoc />
    public EndpointConnectionOrigin Origin =>
        EndpointConnectionOrigin.Configured;

    /// <inheritdoc />
    public EndpointId? ExpectedEndpointId
    {
        get;
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
            expectedEndpointId);
    }
}
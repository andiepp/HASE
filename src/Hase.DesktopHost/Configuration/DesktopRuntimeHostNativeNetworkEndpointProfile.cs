using Hase.Core.Domain.Identity;

namespace Hase.DesktopHost.Configuration;

public sealed record DesktopRuntimeHostNativeNetworkEndpointProfile
{
    public DesktopRuntimeHostNativeNetworkEndpointProfile(
        string expectedEndpointId,
        string host,
        int port)
    {
        ExpectedEndpointId = new EndpointId(expectedEndpointId).Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        ExpectedEndpointId = expectedEndpointId.Trim();
        Host = host.Trim();
        Port = port;
    }

    public string ExpectedEndpointId { get; }
    public string Host { get; }
    public int Port { get; }

    public override string ToString() =>
        $"Native network endpoint '{ExpectedEndpointId}'";
}

using System.Net;

namespace Hase.Transport.Discovery;

internal interface IMdnsServiceBrowser
    : IDisposable
{
    event EventHandler<
        MdnsServiceAnnouncementEventArgs>?
            AnnouncementReceived;

    void Start();
}

internal sealed class MdnsServiceAnnouncementEventArgs
    : EventArgs
{
    public MdnsServiceAnnouncementEventArgs(
        string? serviceInstanceName,
        IEnumerable<IPAddress>? addresses,
        int port)
    {
        ServiceInstanceName =
            serviceInstanceName;

        Addresses =
            addresses?.ToArray();

        Port =
            port;
    }

    public string? ServiceInstanceName
    {
        get;
    }

    public IReadOnlyList<IPAddress>? Addresses
    {
        get;
    }

    public int Port
    {
        get;
    }
}
using Tmds.MDns;

namespace Hase.Transport.Discovery;

internal sealed class TmdsMdnsServiceBrowser
    : IMdnsServiceBrowser
{
    private readonly ServiceBrowser _serviceBrowser;

    private bool _started;

    private bool _disposed;

    public TmdsMdnsServiceBrowser()
    {
        _serviceBrowser =
            new ServiceBrowser();

        _serviceBrowser.ServiceAdded +=
            OnServiceAnnouncement;

        _serviceBrowser.ServiceChanged +=
            OnServiceAnnouncement;
    }

    public event EventHandler<
        MdnsServiceAnnouncementEventArgs>?
            AnnouncementReceived;

    public void Start()
    {
        ObjectDisposedException
            .ThrowIf(
                _disposed,
                this);

        if (_started)
        {
            throw new InvalidOperationException(
                "The mDNS service browser has already been started.");
        }

        _serviceBrowser.StartBrowse(
            MdnsNetworkEndpointBrowser.ServiceType,
            useSynchronizationContext: false);

        _started =
            true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _serviceBrowser.ServiceAdded -=
            OnServiceAnnouncement;

        _serviceBrowser.ServiceChanged -=
            OnServiceAnnouncement;

        if (_started)
        {
            _serviceBrowser.StopBrowse();
        }

        _disposed =
            true;
    }

    private void OnServiceAnnouncement(
        object? sender,
        ServiceAnnouncementEventArgs eventArgs)
    {
        ServiceAnnouncement announcement =
            eventArgs.Announcement;

        AnnouncementReceived?.Invoke(
            this,
            new MdnsServiceAnnouncementEventArgs(
                announcement.Instance,
                announcement.Addresses,
                announcement.Port));
    }
}
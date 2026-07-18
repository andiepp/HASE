using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;

namespace Hase.Transport.Discovery;

/// <summary>
/// Browses for HASE TCP endpoints advertised through mDNS/DNS-SD.
/// </summary>
public sealed class MdnsNetworkEndpointBrowser
    : INetworkEndpointBrowser
{
    internal const string ServiceType =
        "_hase._tcp";

    private readonly Func<
        IMdnsServiceBrowser> _serviceBrowserFactory;

    /// <summary>
    /// Initializes an mDNS network endpoint browser.
    /// </summary>
    public MdnsNetworkEndpointBrowser()
        : this(
            static () =>
                new TmdsMdnsServiceBrowser())
    {
    }

    internal MdnsNetworkEndpointBrowser(
        Func<IMdnsServiceBrowser> serviceBrowserFactory)
    {
        ArgumentNullException.ThrowIfNull(
            serviceBrowserFactory);

        _serviceBrowserFactory =
            serviceBrowserFactory;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<
        NetworkEndpointCandidate> BrowseAsync(
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
    {
        IMdnsServiceBrowser serviceBrowser =
            _serviceBrowserFactory()
            ?? throw new InvalidOperationException(
                "The mDNS service browser factory returned null.");

        using (serviceBrowser)
        {
            var channel =
                Channel.CreateUnbounded<
                    NetworkEndpointCandidate>(
                        new UnboundedChannelOptions
                        {
                            SingleReader = true,
                            SingleWriter = false
                        });

            var observedCandidates =
                new HashSet<
                    NetworkEndpointCandidate>();

            var observedCandidatesLock =
                new object();

            EventHandler<
                MdnsServiceAnnouncementEventArgs>
                    announcementHandler =
                        (
                            sender,
                            eventArgs) =>
                        {
                            IReadOnlyList<
                                NetworkEndpointCandidate> candidates =
                                    CreateCandidates(
                                        eventArgs
                                            .ServiceInstanceName,
                                        eventArgs
                                            .Addresses,
                                        eventArgs
                                            .Port);

                            foreach (
                                NetworkEndpointCandidate candidate
                                in candidates)
                            {
                                bool added;

                                lock (observedCandidatesLock)
                                {
                                    added =
                                        observedCandidates.Add(
                                            candidate);
                                }

                                if (added)
                                {
                                    channel.Writer.TryWrite(
                                        candidate);
                                }
                            }
                        };

            serviceBrowser.AnnouncementReceived +=
                announcementHandler;

            try
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                serviceBrowser.Start();

                await foreach (
                    NetworkEndpointCandidate candidate
                    in channel.Reader.ReadAllAsync(
                        cancellationToken))
                {
                    yield return candidate;
                }
            }
            finally
            {
                serviceBrowser.AnnouncementReceived -=
                    announcementHandler;

                channel.Writer.TryComplete();
            }
        }
    }

    internal static IReadOnlyList<
        NetworkEndpointCandidate> CreateCandidates(
            string? serviceInstanceName,
            IEnumerable<IPAddress>? addresses,
            int port)
    {
        if (string.IsNullOrWhiteSpace(
            serviceInstanceName))
        {
            return Array.Empty<
                NetworkEndpointCandidate>();
        }

        if (addresses is null)
        {
            return Array.Empty<
                NetworkEndpointCandidate>();
        }

        if (port is < 1 or > 65535)
        {
            return Array.Empty<
                NetworkEndpointCandidate>();
        }

        var candidates =
            new HashSet<
                NetworkEndpointCandidate>();

        foreach (
            IPAddress? address
            in addresses)
        {
            if (address is null)
            {
                continue;
            }

            if (address.AddressFamily
                != AddressFamily.InterNetwork)
            {
                continue;
            }

            candidates.Add(
                new NetworkEndpointCandidate(
                    serviceInstanceName,
                    address,
                    port));
        }

        return candidates.ToArray();
    }
}
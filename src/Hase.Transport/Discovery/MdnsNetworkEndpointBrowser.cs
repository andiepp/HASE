using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using Tmds.MDns;

namespace Hase.Transport.Discovery;

/// <summary>
/// Browses for HASE TCP endpoints advertised through mDNS/DNS-SD.
/// </summary>
public sealed class MdnsNetworkEndpointBrowser
    : INetworkEndpointBrowser
{
    internal const string ServiceType =
        "_hase._tcp";

    /// <inheritdoc />
    public async IAsyncEnumerable<
        NetworkEndpointCandidate> BrowseAsync(
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
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
            new HashSet<NetworkEndpointCandidate>();

        var observedCandidatesLock =
            new object();

        var serviceBrowser =
            new ServiceBrowser();

        EventHandler<
            ServiceAnnouncementEventArgs> announcementHandler =
                (
                    sender,
                    eventArgs) =>
                {
                    IReadOnlyList<
                        NetworkEndpointCandidate> candidates =
                            CreateCandidates(
                                eventArgs.Announcement.Instance,
                                eventArgs.Announcement.Addresses,
                                eventArgs.Announcement.Port);

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

        serviceBrowser.ServiceAdded +=
            announcementHandler;

        serviceBrowser.ServiceChanged +=
            announcementHandler;

        try
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            serviceBrowser.StartBrowse(
                ServiceType,
                useSynchronizationContext: false);

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
            serviceBrowser.ServiceAdded -=
                announcementHandler;

            serviceBrowser.ServiceChanged -=
                announcementHandler;

            serviceBrowser.StopBrowse();

            channel.Writer.TryComplete();
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
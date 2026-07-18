using System.Net;
using Hase.Transport.Discovery;

namespace Hase.Transport.Tests;

public sealed class MdnsNetworkEndpointBrowserLifecycleTests
{
    [Fact]
    public async Task BrowseAsync_Announcement_ShouldDeliverCandidate()
    {
        // Arrange
        var serviceBrowser =
            new StubMdnsServiceBrowser();

        var browser =
            new MdnsNetworkEndpointBrowser(
                () => serviceBrowser);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await using IAsyncEnumerator<
            NetworkEndpointCandidate> enumerator =
                browser
                    .BrowseAsync(
                        cancellationTokenSource.Token)
                    .GetAsyncEnumerator();

        Task<bool> moveNextTask =
            enumerator
                .MoveNextAsync()
                .AsTask();

        await serviceBrowser.Started;

        // Act
        serviceBrowser.Publish(
            "doit-esp32-devkitc-v4-01",
            IPAddress.Parse(
                "192.168.0.223"),
            5000);

        bool candidateAvailable =
            await moveNextTask;

        // Assert
        Assert.True(
            candidateAvailable);

        Assert.Equal(
            IPAddress.Parse(
                "192.168.0.223"),
            enumerator.Current.Address);

        Assert.Equal(
            5000,
            enumerator.Current.Port);

        cancellationTokenSource.Cancel();
    }

    [Fact]
    public async Task BrowseAsync_Cancellation_ShouldDisposeServiceBrowser()
    {
        // Arrange
        var serviceBrowser =
            new StubMdnsServiceBrowser();

        var browser =
            new MdnsNetworkEndpointBrowser(
                () => serviceBrowser);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await using IAsyncEnumerator<
            NetworkEndpointCandidate> enumerator =
                browser
                    .BrowseAsync(
                        cancellationTokenSource.Token)
                    .GetAsyncEnumerator();

        Task<bool> moveNextTask =
            enumerator
                .MoveNextAsync()
                .AsTask();

        await serviceBrowser.Started;

        // Act
        cancellationTokenSource.Cancel();

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                () => moveNextTask);

        Assert.True(
            serviceBrowser.Disposed);
    }

    [Fact]
    public async Task BrowseAsync_Cancellation_ShouldRemoveEventHandler()
    {
        // Arrange
        var serviceBrowser =
            new StubMdnsServiceBrowser();

        var browser =
            new MdnsNetworkEndpointBrowser(
                () => serviceBrowser);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await using IAsyncEnumerator<
            NetworkEndpointCandidate> enumerator =
                browser
                    .BrowseAsync(
                        cancellationTokenSource.Token)
                    .GetAsyncEnumerator();

        Task<bool> moveNextTask =
            enumerator
                .MoveNextAsync()
                .AsTask();

        await serviceBrowser.Started;

        Assert.Equal(
            1,
            serviceBrowser.HandlerCount);

        // Act
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                () => moveNextTask);

        // Assert
        Assert.Equal(
            0,
            serviceBrowser.HandlerCount);
    }

    [Fact]
    public async Task BrowseAsync_SeparateDuplicateAnnouncements_ShouldDeliverOnce()
    {
        // Arrange
        var serviceBrowser =
            new StubMdnsServiceBrowser();

        var browser =
            new MdnsNetworkEndpointBrowser(
                () => serviceBrowser);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await using IAsyncEnumerator<
            NetworkEndpointCandidate> enumerator =
                browser
                    .BrowseAsync(
                        cancellationTokenSource.Token)
                    .GetAsyncEnumerator();

        Task<bool> firstMoveNextTask =
            enumerator
                .MoveNextAsync()
                .AsTask();

        await serviceBrowser.Started;

        serviceBrowser.Publish(
            "doit-esp32-devkitc-v4-01",
            IPAddress.Parse(
                "192.168.0.223"),
            5000);

        Assert.True(
            await firstMoveNextTask);

        Task<bool> secondMoveNextTask =
            enumerator
                .MoveNextAsync()
                .AsTask();

        // Act
        serviceBrowser.Publish(
            "renamed-service-instance",
            IPAddress.Parse(
                "192.168.0.223"),
            5000);

        serviceBrowser.Publish(
            "another-endpoint",
            IPAddress.Parse(
                "192.168.0.224"),
            5000);

        // Assert
        Assert.True(
            await secondMoveNextTask);

        Assert.Equal(
            IPAddress.Parse(
                "192.168.0.224"),
            enumerator.Current.Address);

        cancellationTokenSource.Cancel();
    }

    [Fact]
    public async Task BrowseAsync_InvalidAnnouncement_ShouldRemainActive()
    {
        // Arrange
        var serviceBrowser =
            new StubMdnsServiceBrowser();

        var browser =
            new MdnsNetworkEndpointBrowser(
                () => serviceBrowser);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await using IAsyncEnumerator<
            NetworkEndpointCandidate> enumerator =
                browser
                    .BrowseAsync(
                        cancellationTokenSource.Token)
                    .GetAsyncEnumerator();

        Task<bool> moveNextTask =
            enumerator
                .MoveNextAsync()
                .AsTask();

        await serviceBrowser.Started;

        // Act
        serviceBrowser.Publish(
            "",
            IPAddress.Parse(
                "192.168.0.223"),
            5000);

        serviceBrowser.Publish(
            "doit-esp32-devkitc-v4-01",
            IPAddress.Parse(
                "192.168.0.223"),
            5000);

        // Assert
        Assert.True(
            await moveNextTask);

        Assert.Equal(
            "doit-esp32-devkitc-v4-01",
            enumerator.Current.ServiceInstanceName);

        cancellationTokenSource.Cancel();
    }

    private sealed class StubMdnsServiceBrowser
        : IMdnsServiceBrowser
    {
        private readonly TaskCompletionSource _started =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        private EventHandler<
            MdnsServiceAnnouncementEventArgs>?
                _announcementReceived;

        public event EventHandler<
            MdnsServiceAnnouncementEventArgs>?
                AnnouncementReceived
        {
            add
            {
                _announcementReceived +=
                    value;

                HandlerCount++;
            }

            remove
            {
                _announcementReceived -=
                    value;

                HandlerCount--;
            }
        }

        public Task Started =>
            _started.Task;

        public bool Disposed
        {
            get;
            private set;
        }

        public int HandlerCount
        {
            get;
            private set;
        }

        public void Start()
        {
            _started.TrySetResult();
        }

        public void Publish(
            string? serviceInstanceName,
            IPAddress address,
            int port)
        {
            _announcementReceived?.Invoke(
                this,
                new MdnsServiceAnnouncementEventArgs(
                    serviceInstanceName,
                    new[]
                    {
                        address
                    },
                    port));
        }

        public void Dispose()
        {
            Disposed =
                true;
        }
    }
}
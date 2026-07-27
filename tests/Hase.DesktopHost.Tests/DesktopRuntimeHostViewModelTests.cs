using System.ComponentModel;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostViewModelTests
{
    [Fact]
    public async Task StartAsync_ShouldProjectRunningStatusAndNotifyBindings()
    {
        var host = new RecordingRuntimeHost();
        using var viewModel = CreateViewModel(host);
        var changedProperties = RecordChangedProperties(viewModel);

        await viewModel.StartAsync();

        Assert.Equal(DesktopRuntimeHostStatus.Running, viewModel.Status);
        Assert.Equal("Running", viewModel.StatusText);
        Assert.False(viewModel.HasError);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Contains(nameof(DesktopRuntimeHostViewModel.Status), changedProperties);
        Assert.Contains(nameof(DesktopRuntimeHostViewModel.StatusText), changedProperties);
    }

    [Fact]
    public async Task StartAsync_WhenHostFaults_ShouldProjectError()
    {
        var expected = new InvalidOperationException("Runtime composition failed.");
        var host = new RecordingRuntimeHost { StartException = expected };
        using var viewModel = CreateViewModel(host);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.StartAsync());

        Assert.Same(expected, actual);
        Assert.Equal(DesktopRuntimeHostStatus.Faulted, viewModel.Status);
        Assert.True(viewModel.HasError);
        Assert.Equal(expected.Message, viewModel.ErrorMessage);
    }

    [Fact]
    public void ShellInformation_ShouldRemainExplicitlyPresentationOnly()
    {
        var information = CreateShellInformation();
        using var viewModel = new DesktopRuntimeHostViewModel(
            new RecordingRuntimeHost(),
            information);

        Assert.Same(information, viewModel.ShellInformation);
        Assert.Equal("Shell validation backend", viewModel.ShellInformation.Composition);
        Assert.Equal("Not configured", viewModel.ShellInformation.LoopbackBinding);
        Assert.Equal("Not configured", viewModel.ShellInformation.PrivateNetworkBinding);
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromRuntimeHostChanges()
    {
        var host = new RecordingRuntimeHost();
        var viewModel = CreateViewModel(host);
        var changedProperties = RecordChangedProperties(viewModel);

        viewModel.Dispose();
        host.SetStatus(DesktopRuntimeHostStatus.Running);

        Assert.Empty(changedProperties);
    }

    private static DesktopRuntimeHostViewModel CreateViewModel(
        RecordingRuntimeHost host) =>
        new(host, CreateShellInformation());

    private static DesktopRuntimeHostShellInformation CreateShellInformation() =>
        new(
            Composition: "Shell validation backend",
            HostIdentity: "Not available",
            ApiVersion: "Not available",
            LoopbackBinding: "Not configured",
            PrivateNetworkBinding: "Not configured");

    private static List<string?> RecordChangedProperties(
        INotifyPropertyChanged source)
    {
        var properties = new List<string?>();
        source.PropertyChanged += (_, eventArgs) => properties.Add(eventArgs.PropertyName);
        return properties;
    }

    private sealed class RecordingRuntimeHost : IDesktopRuntimeHost
    {
        public DesktopRuntimeHostStatus Status { get; private set; }

        public Exception? LastError { get; private set; }

        public Exception? StartException { get; init; }

        public event EventHandler<DesktopRuntimeHostStatusChangedEventArgs>? StatusChanged;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetStatus(DesktopRuntimeHostStatus.Starting);

            if (StartException is not null)
            {
                LastError = StartException;
                SetStatus(DesktopRuntimeHostStatus.Faulted);
                return Task.FromException(StartException);
            }

            SetStatus(DesktopRuntimeHostStatus.Running);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetStatus(DesktopRuntimeHostStatus.Stopping);
            SetStatus(DesktopRuntimeHostStatus.Stopped);
            return Task.CompletedTask;
        }

        public void SetStatus(DesktopRuntimeHostStatus status)
        {
            var previousStatus = Status;
            Status = status;
            StatusChanged?.Invoke(
                this,
                new DesktopRuntimeHostStatusChangedEventArgs(previousStatus, status));
        }
    }
}

using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Tests;

public sealed class MainWindowMediaWindowTests
{
    [Fact]
    public void OpenMediaCommand_WithoutMediaSources_ShouldBeDisabled()
    {
        var controller = new FakeMediaWindowController();
        var viewModel = new MainWindowViewModel();
        viewModel.Configure(
            new StubSessionController(),
            new StubConfigurationFilePicker(),
            mediaController: controller);

        Assert.False(viewModel.OpenMediaCommand.CanExecute());
        Assert.Equal(0, controller.OpenCount);
    }

    private sealed class FakeMediaWindowController : IClientMediaWindowController
    {
        public int OpenCount { get; private set; }

        public void Open() => OpenCount++;

        public void Close()
        {
        }
    }

    private sealed class StubSessionController : IRuntimeHostClientSessionController
    {
        public Task ConnectAsync(
            string configurationFilePath,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DisconnectAsync() =>
            Task.CompletedTask;

        public Task<RemotePropertyOperationResult> ReadPropertyAsync(
            RemotePropertyTarget target,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RemotePropertyOperationResult> WritePropertyAsync(
            RemotePropertyTarget target,
            RemoteValue requestedValue,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RemoteCommandOperationResult> ExecuteCommandAsync(
            RemoteCommandExecutionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubConfigurationFilePicker : IClientConfigurationFilePicker
    {
        public string? PickConfigurationFile() => null;
    }
}

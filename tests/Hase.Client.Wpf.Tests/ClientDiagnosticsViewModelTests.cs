using Hase.Client;
using Hase.Client.Diagnostics;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Tests;

public sealed class ClientDiagnosticsViewModelTests
{
    [Fact]
    public void ProtocolFilter_IsCumulative()
    {
        BoundedClientDiagnosticCollector collector =
            new(10, ClientDiagnosticLevel.Protocol);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateEvent("Operational", ClientDiagnosticCategory.ClientConnection));
        publisher.Publish(
            new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Protocol,
                ClientDiagnosticCategory.NorthboundExchange,
                "Protocol"));
        var viewModel = new ClientDiagnosticsViewModel(collector);

        viewModel.SelectedLevelFilter = nameof(ClientDiagnosticLevel.Protocol);

        Assert.Equal(new[] { "Operational", "Protocol" }, viewModel.Records.Select(record => record.EventName));
    }

    [Fact]
    public void BytesFilter_ShowsAvailableProjectedLevels()
    {
        BoundedClientDiagnosticCollector collector =
            new(10, ClientDiagnosticLevel.Protocol);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateEvent("Operational", ClientDiagnosticCategory.ClientConnection));
        var viewModel = new ClientDiagnosticsViewModel(collector);

        viewModel.SelectedLevelFilter = nameof(ClientDiagnosticLevel.Bytes);

        Assert.False(viewModel.IsBytesUnavailable);
        Assert.Empty(viewModel.BytesUnavailableMessage);
        Assert.Single(viewModel.Records);
    }

    [Fact]
    public void Pause_CaptureContinuesWhileProjectionAndSelectionRemainFrozen()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateEvent("Before", ClientDiagnosticCategory.ClientConnection));
        var viewModel = new ClientDiagnosticsViewModel(collector);
        ClientDiagnosticRecord selected = viewModel.SelectedRecord!;

        viewModel.PauseCommand.Execute();
        publisher.Publish(CreateEvent("During", ClientDiagnosticCategory.ClientConnection));
        viewModel.Refresh();

        Assert.True(viewModel.IsPaused);
        Assert.Equal("Paused", viewModel.PresentationState);
        Assert.Equal("Before", Assert.Single(viewModel.Records).EventName);
        Assert.Same(selected, viewModel.SelectedRecord);
        Assert.Equal(1, viewModel.PendingRecordCount);
        Assert.Equal(2, collector.GetSnapshot().Records.Count);
    }

    [Fact]
    public void Resume_ReconcilesCurrentRetainedSnapshot()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateEvent("Before", ClientDiagnosticCategory.ClientConnection));
        var viewModel = new ClientDiagnosticsViewModel(collector);
        viewModel.PauseCommand.Execute();
        publisher.Publish(CreateEvent("During", ClientDiagnosticCategory.ClientConnection));
        viewModel.Refresh();

        viewModel.ResumeCommand.Execute();

        Assert.False(viewModel.IsPaused);
        Assert.Equal("Running", viewModel.PresentationState);
        Assert.Equal(new[] { "Before", "During" }, viewModel.Records.Select(record => record.EventName));
        Assert.Equal(0, viewModel.PendingRecordCount);
    }

    [Fact]
    public void Pause_OverCapacity_ResumeShowsOnlyCurrentlyRetainedRecords()
    {
        BoundedClientDiagnosticCollector collector = new(2);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateEvent("Before", ClientDiagnosticCategory.ClientConnection));
        var viewModel = new ClientDiagnosticsViewModel(collector);
        viewModel.PauseCommand.Execute();
        publisher.Publish(CreateEvent("During1", ClientDiagnosticCategory.ClientConnection));
        publisher.Publish(CreateEvent("During2", ClientDiagnosticCategory.ClientConnection));
        publisher.Publish(CreateEvent("During3", ClientDiagnosticCategory.ClientConnection));
        viewModel.Refresh();

        viewModel.ResumeCommand.Execute();

        Assert.Equal(new[] { "During2", "During3" }, viewModel.Records.Select(record => record.EventName));
        Assert.Equal(2, viewModel.EvictedRecordCount);
    }

    [Fact]
    public void ClearWhilePaused_ClearsFrozenProjectionAndPreservesPausedState()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateEvent("Before", ClientDiagnosticCategory.ClientConnection));
        var viewModel = new ClientDiagnosticsViewModel(collector);
        viewModel.PauseCommand.Execute();
        publisher.Publish(CreateEvent("During", ClientDiagnosticCategory.ClientConnection));
        viewModel.Refresh();

        viewModel.ClearCommand.Execute();

        Assert.True(viewModel.IsPaused);
        Assert.Empty(viewModel.Records);
        Assert.Empty(collector.GetSnapshot().Records);
        Assert.Equal(0, viewModel.PendingRecordCount);
    }

    [Fact]
    public void FilterWhilePaused_AppliesToFrozenSourceOnly()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateEvent("Connection", ClientDiagnosticCategory.ClientConnection));
        publisher.Publish(CreateEvent("Property", ClientDiagnosticCategory.ClientProperty));
        var viewModel = new ClientDiagnosticsViewModel(collector);
        viewModel.PauseCommand.Execute();
        publisher.Publish(CreateEvent("LaterProperty", ClientDiagnosticCategory.ClientProperty));

        viewModel.SelectedCategoryFilter = nameof(ClientDiagnosticCategory.ClientProperty);

        Assert.Equal("Property", Assert.Single(viewModel.Records).EventName);
    }

    [Fact]
    public void SameViewModel_PreservesPauseAndFilterAcrossWindowIndependentUse()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        var viewModel = new ClientDiagnosticsViewModel(collector);
        viewModel.SelectedCategoryFilter = nameof(ClientDiagnosticCategory.ClientObservation);
        viewModel.PauseCommand.Execute();

        ClientDiagnosticsViewModel reopened = viewModel;

        Assert.True(reopened.IsPaused);
        Assert.Equal(nameof(ClientDiagnosticCategory.ClientObservation), reopened.SelectedCategoryFilter);
    }

    [Fact]
    public void Refresh_ProjectsRecordsAndSelectsNewest()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateEvent("First", ClientDiagnosticCategory.ClientConnection));
        publisher.Publish(CreateEvent("Second", ClientDiagnosticCategory.ClientProperty));

        var viewModel = new ClientDiagnosticsViewModel(collector);

        Assert.Equal(2, viewModel.RecordCount);
        Assert.Equal("Second", viewModel.SelectedRecord!.EventName);
    }

    [Fact]
    public void CategoryFilter_ChangesProjectionWithoutChangingCapture()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateEvent("Connection", ClientDiagnosticCategory.ClientConnection));
        publisher.Publish(CreateEvent("Property", ClientDiagnosticCategory.ClientProperty));
        var viewModel = new ClientDiagnosticsViewModel(collector);

        viewModel.SelectedCategoryFilter = nameof(ClientDiagnosticCategory.ClientProperty);

        Assert.Equal("Property", Assert.Single(viewModel.Records).EventName);
        Assert.Equal(2, collector.GetSnapshot().Records.Count);
    }

    [Fact]
    public void ClearCommand_ClearsCollectorAndProjection()
    {
        BoundedClientDiagnosticCollector collector = new(1);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateEvent("First", ClientDiagnosticCategory.ClientConnection));
        publisher.Publish(CreateEvent("Second", ClientDiagnosticCategory.ClientConnection));
        var viewModel = new ClientDiagnosticsViewModel(collector);
        Assert.Equal(1, viewModel.EvictedRecordCount);

        viewModel.ClearCommand.Execute();

        Assert.Empty(viewModel.Records);
        Assert.Equal(0, viewModel.EvictedRecordCount);
        Assert.Empty(collector.GetSnapshot().Records);
    }

    [Fact]
    public void MetadataText_IsDeterministicAndContainsNoSyntheticValues()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(
            new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Operational,
                ClientDiagnosticCategory.ClientConnection,
                "Connected",
                metadata: new Dictionary<string, string>
                {
                    ["Zeta"] = "2",
                    ["Alpha"] = "1"
                }));
        var viewModel = new ClientDiagnosticsViewModel(collector);

        Assert.Equal($"Alpha: 1{Environment.NewLine}Zeta: 2", viewModel.MetadataText);
    }

    [Fact]
    public void SelectedRemoteBytes_ShouldExposeSummaryAndHex()
    {
        BoundedClientDiagnosticCollector collector =
            new(10, ClientDiagnosticLevel.Bytes);
        new ClientDiagnosticPublisher(collector).Publish(
            new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Bytes,
                ClientDiagnosticCategory.NorthboundBytes,
                "BytesReceived",
                byteSnapshot: new RemoteRuntimeDiagnosticByteSnapshot(
                    4,
                    [0x01, 0xAB],
                    true)));

        var viewModel = new ClientDiagnosticsViewModel(collector);

        Assert.Equal("2/4 bytes (truncated)", viewModel.SelectedByteSummary);
        Assert.Equal("01AB", viewModel.SelectedByteHex);
    }

    private static ClientDiagnosticEvent CreateEvent(
        string eventName,
        ClientDiagnosticCategory category) =>
        new(ClientDiagnosticLevel.Operational, category, eventName);
}

using Hase.Client.Diagnostics;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Tests;

public sealed class ClientDiagnosticsViewModelTests
{
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

    private static ClientDiagnosticEvent CreateEvent(
        string eventName,
        ClientDiagnosticCategory category) =>
        new(ClientDiagnosticLevel.Operational, category, eventName);
}

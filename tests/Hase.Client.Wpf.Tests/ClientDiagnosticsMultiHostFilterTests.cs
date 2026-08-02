using Hase.Client.Configuration;
using Hase.Client.Diagnostics;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Tests;

public sealed class ClientDiagnosticsMultiHostFilterTests
{
    [Fact]
    public void ConfigureRuntimeHosts_PreservesRegistryOrderIncludingDisabledProfiles()
    {
        ClientDiagnosticsViewModel viewModel = CreateViewModel();

        viewModel.ConfigureRuntimeHosts(CreateRegistry());

        Assert.Equal(
            new[] { "All Runtime Hosts", "Alpha Host", "Beta Host" },
            viewModel.RuntimeHostFilters.Select(item => item.DisplayName));
    }

    [Fact]
    public void AllRuntimeHosts_IncludesQualifiedAndContextFreeRecords()
    {
        (ClientDiagnosticsViewModel viewModel, ClientDiagnosticPublisher publisher) = CreatePopulatedViewModel();
        publisher.Publish(CreateEvent("ContextFree"));
        viewModel.Refresh();

        Assert.Equal(
            new[] { "Alpha", "Beta", "ContextFree" },
            viewModel.Records.Select(record => record.EventName));
    }

    [Fact]
    public void SelectedRuntimeHost_IncludesOnlyMatchingQualifiedRecords()
    {
        (ClientDiagnosticsViewModel viewModel, ClientDiagnosticPublisher publisher) = CreatePopulatedViewModel();
        publisher.Publish(CreateEvent("ContextFree"));
        viewModel.Refresh();

        viewModel.SelectedRuntimeHostFilter = viewModel.RuntimeHostFilters[1];

        ClientDiagnosticRecord record = Assert.Single(viewModel.Records);
        Assert.Equal("Alpha", record.EventName);
        Assert.Equal("alpha", record.RuntimeHostProfileId);
    }

    [Fact]
    public void SelectedRuntimeHostFilter_RejectsItemOutsideConfiguredSet()
    {
        ClientDiagnosticsViewModel viewModel = CreateViewModel();

        Assert.Throws<ArgumentException>(() => viewModel.SelectedRuntimeHostFilter =
            new RuntimeHostDiagnosticFilterItem("Alpha Host", new RuntimeHostProfileId("alpha")));
    }

    private static (ClientDiagnosticsViewModel ViewModel, ClientDiagnosticPublisher Publisher)
        CreatePopulatedViewModel()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateEvent("Alpha", CreateContext("alpha", "Alpha Host", "expected-alpha")));
        publisher.Publish(CreateEvent("Beta", CreateContext("beta", "Beta Host", "expected-beta")));
        ClientDiagnosticsViewModel viewModel = new(collector);
        viewModel.ConfigureRuntimeHosts(CreateRegistry());
        return (viewModel, publisher);
    }

    private static ClientDiagnosticsViewModel CreateViewModel() =>
        new(new BoundedClientDiagnosticCollector(10));

    private static RuntimeHostProfileRegistry CreateRegistry() =>
        new(new[]
        {
            new RuntimeHostProfile(
                new RuntimeHostProfileId("alpha"),
                "Alpha Host",
                new RemoteRuntimeHostId("expected-alpha")),
            new RuntimeHostProfile(
                new RuntimeHostProfileId("beta"),
                "Beta Host",
                new RemoteRuntimeHostId("expected-beta"),
                isEnabled: false)
        });

    private static ClientDiagnosticEvent CreateEvent(
        string eventName,
        ClientDiagnosticSessionContext? context = null) =>
        new(
            ClientDiagnosticLevel.Operational,
            ClientDiagnosticCategory.ClientConnection,
            eventName,
            sessionContext: context);

    private static ClientDiagnosticSessionContext CreateContext(
        string profileId,
        string displayName,
        string expectedRuntimeHostId) =>
        new(
            new RuntimeHostProfileId(profileId),
            displayName,
            new RemoteRuntimeHostId(expectedRuntimeHostId));
}

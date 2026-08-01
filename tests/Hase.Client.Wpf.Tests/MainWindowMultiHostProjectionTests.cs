using Hase.Client.Configuration;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Tests;

public sealed class MainWindowMultiHostProjectionTests
{
    [Fact]
    public void ApplySnapshot_ShouldExposeRuntimeHostsAndNotify()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        var viewModel = new MainWindowViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        viewModel.ConfigureRuntimeHosts(new RuntimeHostProfileRegistry([profile]));

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([Session(profile)]));

        Assert.Single(viewModel.RuntimeHosts);
        Assert.Contains(nameof(MainWindowViewModel.RuntimeHosts), changed);
        Assert.Contains(nameof(MainWindowViewModel.SelectedRuntimeHost), changed);
    }

    [Fact]
    public void Selection_ShouldRemainStableAcrossRefreshAndAllowClear()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        var viewModel = new MainWindowViewModel();
        viewModel.ConfigureRuntimeHosts(new RuntimeHostProfileRegistry([profile]));
        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([Session(profile)]));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([Session(profile)]));
        Assert.Equal(profile.ProfileId, viewModel.SelectedRuntimeHost!.ProfileId);
        viewModel.SelectRuntimeHost(null);
        Assert.Null(viewModel.SelectedRuntimeHost);
    }

    [Fact]
    public void UnknownSelection_ShouldThrow()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        var viewModel = new MainWindowViewModel();
        viewModel.ConfigureRuntimeHosts(new RuntimeHostProfileRegistry([profile]));
        Assert.Throws<ArgumentException>("profileId", () => viewModel.SelectRuntimeHost(new RuntimeHostProfileId("missing")));
    }

    [Fact]
    public void ApplySnapshot_WithoutRegistry_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() => new MainWindowViewModel().ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([])));
    }

    private static RuntimeHostProfile Profile(string id, string host) =>
        new(new RuntimeHostProfileId(id), id, new RemoteRuntimeHostId(host));
    private static RuntimeHostProfileSessionSnapshot Session(RuntimeHostProfile profile) =>
        new(profile, new RuntimeHostClientSessionStatus(RuntimeHostClientSessionState.Disconnected), DateTimeOffset.UtcNow);
}

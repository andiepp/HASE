using Hase.Client.Configuration;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Tests;

public sealed class RuntimeHostProfileItemViewModelTests
{
    [Theory]
    [InlineData(RuntimeHostClientSessionState.Unspecified, "Connect")]
    [InlineData(RuntimeHostClientSessionState.Disconnected, "Connect")]
    [InlineData(RuntimeHostClientSessionState.Disconnecting, "Connect")]
    [InlineData(RuntimeHostClientSessionState.Faulted, "Connect")]
    [InlineData(RuntimeHostClientSessionState.Connecting, "Disconnect")]
    [InlineData(RuntimeHostClientSessionState.Connected, "Disconnect")]
    [InlineData(RuntimeHostClientSessionState.Reconnecting, "Disconnect")]
    public void ConnectionActionLabel_ShouldMatchToggleAction(
        RuntimeHostClientSessionState state,
        string expectedLabel)
    {
        Assert.Equal(expectedLabel, Item(state).ConnectionActionLabel);
    }

    private static RuntimeHostProfileItemViewModel Item(RuntimeHostClientSessionState state) =>
        new(new RuntimeHostProfileId("first"),
            "First",
            true,
            new RemoteRuntimeHostId("host-01"),
            state,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            false);
}

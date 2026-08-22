using Hase.Client.Configuration;

namespace Hase.Client.Wpf.ViewModels;

public sealed record RuntimeHostProfileItemViewModel(
    RuntimeHostProfileId ProfileId,
    string DisplayName,
    bool IsEnabled,
    RemoteRuntimeHostId ExpectedRuntimeHostId,
    RuntimeHostClientSessionState SessionState,
    RemoteRuntimeHostId? AuthoritativeRuntimeHostId,
    RuntimeHostClientFailureCategory? FailureCategory,
    string? FailureMessage,
    DateTimeOffset ChangedAtUtc,
    bool IsSelected)
{
    /// <summary>
    /// Gets the label of the connection toggle for the current session state.
    /// The value matches the action the toggle performs.
    /// </summary>
    public string ConnectionActionLabel =>
        SessionState is RuntimeHostClientSessionState.Connecting
            or RuntimeHostClientSessionState.Connected
            or RuntimeHostClientSessionState.Reconnecting
            ? "Disconnect"
            : "Connect";
}

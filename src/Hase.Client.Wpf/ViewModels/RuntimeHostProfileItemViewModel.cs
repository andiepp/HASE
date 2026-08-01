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
    bool IsSelected);

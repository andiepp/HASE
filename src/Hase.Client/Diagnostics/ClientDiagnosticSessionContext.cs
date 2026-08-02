using Hase.Client.Configuration;

namespace Hase.Client.Diagnostics;

public sealed record ClientDiagnosticSessionContext
{
    public ClientDiagnosticSessionContext(
        RuntimeHostProfileId profileId,
        string profileDisplayName,
        RemoteRuntimeHostId expectedRuntimeHostId,
        RemoteRuntimeHostId? authoritativeRuntimeHostId = null)
    {
        ProfileId = profileId ?? throw new ArgumentNullException(nameof(profileId));
        if (string.IsNullOrWhiteSpace(profileDisplayName))
            throw new ArgumentException("The profile display name must not be empty.", nameof(profileDisplayName));
        ProfileDisplayName = profileDisplayName.Trim();
        ExpectedRuntimeHostId = expectedRuntimeHostId ?? throw new ArgumentNullException(nameof(expectedRuntimeHostId));
        AuthoritativeRuntimeHostId = authoritativeRuntimeHostId;
    }
    public RuntimeHostProfileId ProfileId { get; }
    public string ProfileDisplayName { get; }
    public RemoteRuntimeHostId ExpectedRuntimeHostId { get; }
    public RemoteRuntimeHostId? AuthoritativeRuntimeHostId { get; }
}

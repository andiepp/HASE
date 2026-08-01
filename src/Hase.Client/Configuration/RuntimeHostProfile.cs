namespace Hase.Client.Configuration;

/// <summary>
/// Defines one transport-independent expected runtime-host profile.
/// </summary>
public sealed record RuntimeHostProfile
{
    public const int MaximumDisplayNameLength =
        256;

    public RuntimeHostProfile(
        RuntimeHostProfileId profileId,
        string displayName,
        RemoteRuntimeHostId expectedRuntimeHostId,
        bool isEnabled = true)
    {
        ProfileId =
            profileId
            ?? throw new ArgumentNullException(
                nameof(profileId));

        ArgumentNullException.ThrowIfNull(
            displayName);

        string normalizedDisplayName =
            displayName.Trim();

        if (normalizedDisplayName.Length == 0)
        {
            throw new ArgumentException(
                "The runtime-host profile display name must not be empty or whitespace.",
                nameof(displayName));
        }

        if (normalizedDisplayName.Length > MaximumDisplayNameLength)
        {
            throw new ArgumentException(
                $"The runtime-host profile display name must not exceed {MaximumDisplayNameLength} characters.",
                nameof(displayName));
        }

        DisplayName =
            normalizedDisplayName;
        ExpectedRuntimeHostId =
            expectedRuntimeHostId
            ?? throw new ArgumentNullException(
                nameof(expectedRuntimeHostId));
        IsEnabled =
            isEnabled;
    }

    public RuntimeHostProfileId ProfileId
    {
        get;
    }

    public string DisplayName
    {
        get;
    }

    public RemoteRuntimeHostId ExpectedRuntimeHostId
    {
        get;
    }

    public bool IsEnabled
    {
        get;
    }
}

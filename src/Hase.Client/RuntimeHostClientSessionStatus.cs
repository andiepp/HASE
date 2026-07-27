namespace Hase.Client;

/// <summary>
/// Represents one immutable normalized client-session status.
/// </summary>
public sealed record RuntimeHostClientSessionStatus
{
    /// <summary>
    /// Initializes one client-session status.
    /// </summary>
    /// <param name="state">
    /// The normalized remote client-session state.
    /// </param>
    /// <param name="runtimeHostId">
    /// The remote runtime-host identity retained from an authoritative initial
    /// snapshot, when one is available.
    /// </param>
    /// <param name="apiVersion">
    /// The remote API version retained from the same authoritative initial
    /// snapshot, when one is available.
    /// </param>
    public RuntimeHostClientSessionStatus(
        RuntimeHostClientSessionState state,
        RemoteRuntimeHostId? runtimeHostId = null,
        RuntimeHostClientApiVersion? apiVersion = null)
    {
        if (!Enum.IsDefined(
                state)
            || state == RuntimeHostClientSessionState.Unspecified)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "A specified client-session state is required.");
        }

        if ((runtimeHostId is null)
            != (apiVersion is null))
        {
            throw new ArgumentException(
                "Runtime-host identity and API version must either both be "
                + "present or both be absent.");
        }

        if (apiVersion is
            {
                Major: 0
            })
        {
            throw new ArgumentException(
                "The retained API version must have a nonzero major version.",
                nameof(apiVersion));
        }

        if (state == RuntimeHostClientSessionState.Connected
            && runtimeHostId is null)
        {
            throw new ArgumentException(
                "A connected client session requires an authoritative "
                + "runtime-host identity and API version.",
                nameof(runtimeHostId));
        }

        if (state is RuntimeHostClientSessionState.Disconnected
            or RuntimeHostClientSessionState.Connecting
            && runtimeHostId is not null)
        {
            throw new ArgumentException(
                "A disconnected or initially connecting client session "
                + "cannot retain an authoritative runtime-host baseline.",
                nameof(runtimeHostId));
        }

        State =
            state;
        RuntimeHostId =
            runtimeHostId;
        ApiVersion =
            apiVersion;
    }

    /// <summary>
    /// Gets the normalized remote client-session state.
    /// </summary>
    public RuntimeHostClientSessionState State
    {
        get;
    }

    /// <summary>
    /// Gets the retained remote runtime-host identity, when available.
    /// </summary>
    public RemoteRuntimeHostId? RuntimeHostId
    {
        get;
    }

    /// <summary>
    /// Gets the retained remote API version, when available.
    /// </summary>
    public RuntimeHostClientApiVersion? ApiVersion
    {
        get;
    }
}

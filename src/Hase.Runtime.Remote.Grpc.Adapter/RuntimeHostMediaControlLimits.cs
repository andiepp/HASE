namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Fixes the version 1 bounds applied before media-control input reaches a
/// future capture or WebRTC boundary.
/// </summary>
public static class RuntimeHostMediaControlLimits
{
    public const int MaximumSourceIdentityUtf8Bytes = 128;

    public const int MaximumSessionIdUtf8Bytes = 128;

    public const int MaximumSessionDescriptionUtf8Bytes = 49_152;

    public const int MaximumIceCandidateUtf8Bytes = 4_096;

    public const int MaximumIceCandidatesPerPeer = 32;

    public const int MaximumNegotiationMessagesPerPeer = 36;

    public const int MaximumPendingDeliveryMessages = 16;

    public const int MaximumNegotiationExchanges = 128;

    public static TimeSpan NegotiationIdleTimeout { get; } =
        TimeSpan.FromSeconds(15);

    public static TimeSpan NegotiationLifetime { get; } =
        TimeSpan.FromSeconds(60);

    public static TimeSpan SessionLeaseDuration { get; } =
        TimeSpan.FromSeconds(30);
}

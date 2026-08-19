using Hase.Runtime.Media;

namespace Hase.DesktopHost.Configuration;

public sealed record DesktopRuntimeHostMediaConfiguration
{
    public DesktopRuntimeHostMediaConfiguration(
        IReadOnlyList<RuntimeHostMediaSourceConfiguration> sources)
        : this(sources, identityKey: null)
    {
    }

    public DesktopRuntimeHostMediaConfiguration(
        IReadOnlyList<RuntimeHostMediaSourceConfiguration> sources,
        byte[]? identityKey)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0 && identityKey is null)
        {
            throw new ArgumentException(
                "At least one configured media source is required.",
                nameof(sources));
        }

        Sources = sources.ToArray();
        if (identityKey is not null &&
            identityKey.Length !=
                RuntimeHostMediaInventoryReconciler.IdentityKeyByteCount)
        {
            throw new ArgumentException(
                "A 256-bit dynamic camera identity key is required.",
                nameof(identityKey));
        }
        IdentityKey = identityKey?.ToArray();
    }

    public IReadOnlyList<RuntimeHostMediaSourceConfiguration> Sources { get; }

    public byte[]? IdentityKey { get; }

    public bool DynamicInventoryEnabled => IdentityKey is not null;

    public override string ToString() =>
        $"Desktop Runtime Host media configuration ({Sources.Count} source(s), "
        + (DynamicInventoryEnabled ? "dynamic" : "static") + ")";
}

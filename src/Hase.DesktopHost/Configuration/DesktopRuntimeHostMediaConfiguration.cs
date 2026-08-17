using Hase.Runtime.Media;

namespace Hase.DesktopHost.Configuration;

public sealed record DesktopRuntimeHostMediaConfiguration
{
    public DesktopRuntimeHostMediaConfiguration(
        IReadOnlyList<RuntimeHostMediaSourceConfiguration> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0)
        {
            throw new ArgumentException(
                "At least one configured media source is required.",
                nameof(sources));
        }

        Sources = sources.ToArray();
    }

    public IReadOnlyList<RuntimeHostMediaSourceConfiguration> Sources { get; }

    public override string ToString() =>
        $"Desktop Runtime Host media configuration ({Sources.Count} source(s))";
}

namespace Hase.Client.Wpf.Services;

/// <summary>
/// Holds the instrument panels composed into this client.
/// </summary>
/// <remarks>
/// The client library hosts no panel of its own. An application composes the
/// panels it ships into this registry, and an instrument becomes operable
/// through a panel only when it declares a panel identifier the registry
/// resolves. A client composed without panels behaves exactly as one that has
/// no panel concept at all.
/// </remarks>
public sealed class ClientInstrumentPanelRegistry
    : IClientInstrumentPanelRegistry
{
    private readonly IReadOnlyDictionary<string, IClientInstrumentPanel> panels;

    public ClientInstrumentPanelRegistry(
        IEnumerable<IClientInstrumentPanel>? panels = null)
    {
        var byPanelId =
            new Dictionary<string, IClientInstrumentPanel>(StringComparer.Ordinal);

        foreach (IClientInstrumentPanel panel in panels ?? [])
        {
            ArgumentNullException.ThrowIfNull(panel, nameof(panels));

            if (string.IsNullOrWhiteSpace(panel.PanelId))
            {
                throw new ArgumentException(
                    "An instrument panel identifier must not be empty.",
                    nameof(panels));
            }

            if (!byPanelId.TryAdd(panel.PanelId.Trim(), panel))
            {
                throw new ArgumentException(
                    "Only one instrument panel may be registered for each "
                    + "panel identifier.",
                    nameof(panels));
            }
        }

        this.panels = byPanelId;
        AvailablePanelIds = byPanelId.Keys.ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlySet<string> AvailablePanelIds { get; }

    /// <inheritdoc />
    public bool TryResolve(
        string panelId,
        out IClientInstrumentPanel panel)
    {
        if (!string.IsNullOrWhiteSpace(panelId)
            && panels.TryGetValue(panelId.Trim(), out IClientInstrumentPanel? resolved))
        {
            panel = resolved;
            return true;
        }

        panel = null!;
        return false;
    }
}
